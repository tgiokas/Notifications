using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Notifications.Infrastructure.Configuration;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public class RabbitMqEmailConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMqSettings _settings;
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqEmailConsumer(IServiceProvider serviceProvider, IOptions<RabbitMqSettings> options)
    {
        _serviceProvider = serviceProvider;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _settings.HostName,
            UserName = _settings.UserName,
            Password = _settings.Password,
            Port = _settings.Port
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.QueueDeclareAsync("notifications.email", durable: true, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var dto = JsonSerializer.Deserialize<NotificationRequestDto>(json);

                if (dto is null) return;

                using var scope = _serviceProvider.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendAsync(dto, stoppingToken);

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Consumer Error] {ex.Message}");
                // TODO: retry or DLQ
            }
        };

        await _channel.BasicConsumeAsync(queue: "notifications.email", autoAck: false, consumer: consumer);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
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
