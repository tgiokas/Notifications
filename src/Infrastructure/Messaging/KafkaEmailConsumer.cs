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
using Confluent.Kafka;

namespace Notifications.Infrastructure.Messaging;

public class KafkaEmailConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaSettings _settings;

    public KafkaEmailConsumer(IServiceProvider serviceProvider, IOptions<KafkaSettings> options)
    {
        _serviceProvider = serviceProvider;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = "email-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        consumer.Subscribe("email");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = consumer.Consume(stoppingToken);
                var dto = JsonSerializer.Deserialize<NotificationRequestDto>(result.Message.Value);

                if (dto is not null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    await sender.SendAsync(dto, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        finally
        {
            consumer.Close();
        }
    }
}

