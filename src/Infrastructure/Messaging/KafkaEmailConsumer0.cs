using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public class KafkaEmailConsumer0 : BackgroundService
{
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaEmailConsumer0> _logger;
    private readonly string _topic = "email"; // Default topic for email notifications

    public KafkaEmailConsumer0(IConfiguration config, IServiceProvider serviceProvider, ILogger<KafkaEmailConsumer0> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

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

        _consumer = new ConsumerBuilder<Ignore  , string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaEmailConsumer started.");
        _consumer.Subscribe(_topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (result != null)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                        NotificationRequestDto? dto;
                        try
                        {
                            dto = JsonSerializer.Deserialize<NotificationRequestDto>(result.Message.Value);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize message: {Message}", result.Message.Value);
                            continue;
                        }

                        if (dto is null)
                        {
                            _logger.LogWarning("Received null DTO after deserialization. Skipping message.");
                            continue;
                        }

                        await sender.SendAsync(dto, stoppingToken);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error");
                }                
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while processing Kafka message");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("KafkaEmailConsumer stopping due to cancellation.");
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
            _logger.LogInformation("KafkaEmailConsumer stopped.");
        }
    }
}
