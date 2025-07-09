using System.Text;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

using Notifications.Infrastructure.Configuration;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public class _RabbitMqPublisher : _IRabbitMqPublisher, IAsyncDisposable
{
    private readonly ILogger<_RabbitMqPublisher> _logger;
    private readonly RabbitMqSettings _options;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly ConcurrentDictionary<string, bool> _declaredQueues = new();

    public _RabbitMqPublisher(
        IOptions<RabbitMqSettings> options,
        ILogger<_RabbitMqPublisher> logger)
    {
        _logger = logger;
        _options = options.Value;

        _factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            AutomaticRecoveryEnabled = true
        };
    }

    public async Task InitializeAsync()
    {
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        // Declare exchange once on startup
        await _channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);
    }

    public async Task PublishMessageAsync(string routingKey, string message, CancellationToken cancellationToken = default)
    {
        if (_channel == null)
        {
            _logger.LogError("RabbitMQ channel is not initialized.");
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");
        }

        try
        {
            // Only declare queues and bind them once
            if (!_declaredQueues.ContainsKey(routingKey))
            {
                var queueName = $"notifications.{routingKey}";

                await _channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                await _channel.QueueBindAsync(
                    queue: queueName,
                    exchange: _options.ExchangeName,
                    routingKey: routingKey);

                _declaredQueues.TryAdd(routingKey, true);
                _logger.LogInformation("Declared and bound queue {Queue} to exchange {Exchange} with routing key {Key}",
                    queueName, _options.ExchangeName, routingKey);
            }

            var body = Encoding.UTF8.GetBytes(message);

            await _channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                body: body);

            _logger.LogInformation("Published message to exchange {Exchange} with routing key {Key}",
                _options.ExchangeName, routingKey);
        }
        catch (AlreadyClosedException ex)
        {
            _logger.LogError(ex, "RabbitMQ connection/channel was closed.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to exchange {Exchange} with key {Key}", _options.ExchangeName, routingKey);
            throw;
        }

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_channel is IAsyncDisposable asyncChannel)
                await asyncChannel.DisposeAsync();
            else
                _channel?.Dispose();

            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing RabbitMQ resources.");
        }
    }
}

