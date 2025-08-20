using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Confluent.Kafka;

using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public class KafkaEmailConsumer0<TKey, TMessage> : BackgroundService
    where TKey : class
    where TMessage : class
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _topic;
    private readonly ILogger<KafkaEmailConsumer0<TKey, TMessage>> _logger;

    public KafkaEmailConsumer0(IConfiguration config, ILogger<KafkaEmailConsumer0<TKey, TMessage>> logger, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _topic = config["Kafka:Topic"] ?? "email";

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

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumer started for key type {KeyType} and message type {MessageType}",
            typeof(TKey).Name, typeof(TMessage).Name);
        try
        {
            _consumer.Subscribe(_topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to topic {Topic}. Kafka consumer will not start.");
            return; // Exit the background service gracefully
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(100));
                    if (result != null)
                    {
                        // Deserialize key if present
                        TKey? key = null;
                        if (!string.IsNullOrEmpty(result.Message.Key))
                        {
                            // If TKey is string, return the raw string
                            if (typeof(TKey) == typeof(string))
                            {
                                key = result.Message.Key as TKey;
                            }
                            else
                            {
                                key = JsonSerializer.Deserialize<TKey>(result.Message.Key);
                            }
                        }

                        using var scope = _serviceProvider.CreateScope();
                        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                        KafkaMessageRaw? raw = JsonSerializer.Deserialize<KafkaMessageRaw>(result.Message.Value);
                        if (raw == null || string.IsNullOrEmpty(raw.Content))
                        {
                            _logger.LogWarning("Received null KafkaMessage or Content after deserialization. Skipping message.");
                            continue;
                        }

                        NotificationRequestDto? content;
                        try
                        {
                            content = JsonSerializer.Deserialize<NotificationRequestDto>(raw.Content);
                            if (content is not null)
                            {
                                var kafkaMessage = new KafkaMessage<NotificationRequestDto>
                                {
                                    Id = raw.Id.ToString(),
                                    Content = content,
                                    Timestamp = raw.Timestamp
                                };

                                await sender.SendAsync(kafkaMessage.Content, stoppingToken);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize Content: {Content}", raw.Content);
                            continue;
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message: {Reason}", ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing message");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
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