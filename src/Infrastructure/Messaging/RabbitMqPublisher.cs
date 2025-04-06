using System.Text;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

using Notifications.Infrastructure.Configuration;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly RabbitMqSettings _options;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly ConcurrentDictionary<string, bool> _declaredQueues = new();

    public RabbitMqPublisher(
        IOptions<RabbitMqSettings> options,
        ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;
        _options = options.Value;

        _factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            //DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };
    }

    public async Task InitializeAsync()
    {
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
    }

    public async Task PublishMessageAsync(string queueName, string message, CancellationToken cancellationToken = default)
    {
        if (_channel == null)
        {
            _logger.LogError("RabbitMQ channel is not initialized.");
            throw new InvalidOperationException("RabbitMQ channel is not initialized.");
        }

        try
        {
            if (!_declaredQueues.ContainsKey(queueName))
            {
                await _channel.QueueDeclareAsync(queue: queueName,
                                      durable: true,
                                      exclusive: false,
                                      autoDelete: false,
                                      arguments: null);

                _declaredQueues.TryAdd(queueName, true);
                _logger.LogInformation("Declared queue {Queue}", queueName);
            }

            var body = Encoding.UTF8.GetBytes(message);

            await _channel.BasicPublishAsync(exchange: "",
                                  routingKey: queueName,                                  
                                  body: body);

            _logger.LogInformation("Published message to queue {Queue}", queueName);
        }
        catch (AlreadyClosedException ex)
        {
            _logger.LogError(ex, "RabbitMQ connection/channel was closed.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to queue {Queue}", queueName);
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
