using System.Text;
using RabbitMQ.Client;
using Notifications.Application.Interfaces;
using Microsoft.Extensions.Options;
using Notifications.Infrastructure.Configuration;

namespace Notifications.Infrastructure.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private IChannel? _channel;
    private IConnection? _connection;
    private readonly ConnectionFactory _factory;

    public RabbitMqPublisher(IOptions<RabbitMqSettings> options)
    {
        _factory = new ConnectionFactory
        {
            HostName = options.Value.HostName,
            UserName = options.Value.UserName,
            Password = options.Value.Password,
            Port = options.Value.Port,
            //DispatchConsumersAsync = true
        };
    }

    public async Task InitializeAsync()
    {
        _connection = await _factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
    }

    public async Task PublishMessageAsync(string queueName, string message)
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ channel is not initialized. Call InitializeAsync() first.");

        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        var body = Encoding.UTF8.GetBytes(message);
        await _channel.BasicPublishAsync(exchange: "", routingKey: queueName, body: body);
       
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is IAsyncDisposable asyncChannel)
            await asyncChannel.DisposeAsync();
        else
            _channel?.Dispose();

        if (_connection is IAsyncDisposable asyncConn)
            await asyncConn.DisposeAsync();
        else
            _connection?.Dispose();
    }
}
