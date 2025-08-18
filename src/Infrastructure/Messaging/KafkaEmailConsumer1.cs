using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public class KafkaEmailConsumer1 : BackgroundService
{
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IServiceProvider _serviceProvider;    
    private readonly ILogger<KafkaEmailConsumer1> _logger;
    private readonly string _topic;

    public KafkaEmailConsumer1(IConfiguration config, ILogger<KafkaEmailConsumer1> logger, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _topic = config["Kafka:Topic"] ?? "TopicA";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "kafka1:9092,kafka2:9092",
            GroupId = config["Kafka:GroupId"] ?? "ConsumerGroupA",
            AutoOffsetReset = Enum.TryParse(config["Kafka:AutoOffsetReset"], out AutoOffsetReset offset) ? offset : AutoOffsetReset.Earliest,
            EnableAutoCommit = bool.TryParse(config["Kafka:EnableAutoCommit"], out var autoCommit) ? autoCommit : true,
            AutoCommitIntervalMs = int.TryParse(config["Kafka:AutoCommitIntervalMs"], out var commitInterval) ? commitInterval : 5000,
            SessionTimeoutMs = int.TryParse(config["Kafka:SessionTimeoutMs"], out var sessionTimeout) ? sessionTimeout : 30000,
            MaxPollIntervalMs = int.TryParse(config["Kafka:MaxPollIntervalMs"], out var maxPoll) ? maxPoll : 300000,
            ApiVersionRequestTimeoutMs = int.TryParse(config["Kafka:ApiVersionRequestTimeoutMs"], out var apiTimeout) ? apiTimeout : 10000,
            ReconnectBackoffMs = int.TryParse(config["Kafka:ReconnectBackoffMs"], out var backoff) ? backoff : 1000,
            ReconnectBackoffMaxMs = int.TryParse(config["Kafka:ReconnectBackoffMaxMs"], out var backoffMax) ? backoffMax : 10000
        };

        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Kafka consumer started");
        _consumer.Subscribe(_topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(stoppingToken);
                    var email = JsonSerializer.Deserialize<NotificationRequestDto>(result.Message.Value);

                    if (email is null)
                    {
                        _logger.LogWarning("Null email payload received");
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    await sender.SendAsync(email, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Deserialization error");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected consumer error");
                }
            }
        }
        finally
        {
            _consumer.Close();
        }
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}