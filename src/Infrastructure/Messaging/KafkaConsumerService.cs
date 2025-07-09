using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using System.Text.Json;

namespace Notifications.Infrastructure.Messaging;

public class KafkaConsumerService<TKey, TMessage> : BackgroundService
    where TKey : class
    where TMessage : class
{
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;
    private readonly MessageStoreService<TMessage> _messageStore;
    private readonly ILogger<KafkaConsumerService<TKey, TMessage>> _logger;

    public KafkaConsumerService(IConfiguration config, MessageStoreService<TMessage> messageStore,
       ILogger<KafkaConsumerService<TKey, TMessage>> logger)
    {
        _messageStore = messageStore;
        _logger = logger;
        _topic = config["Kafka:Topic"] ?? "TopicA";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "kafka1:9092,kafka2:9092",
            GroupId = config["Kafka:GroupId"] ?? "ConsumerGroupA",

            AutoOffsetReset = Enum.TryParse(config["Kafka:AutoOffsetReset"], out AutoOffsetReset autoOffsetReset)
                ? autoOffsetReset
                : AutoOffsetReset.Earliest,
            EnableAutoCommit = bool.TryParse(config["Kafka:EnableAutoCommit"], out var enableAutoCommit)
                ? enableAutoCommit
                : true,
            AutoCommitIntervalMs = int.TryParse(config["Kafka:AutoCommitIntervalMs"], out var autoCommitIntervalMs)
                ? autoCommitIntervalMs
                : 5000,
            SessionTimeoutMs = int.TryParse(config["Kafka:SessionTimeoutMs"], out var sessionTimeoutMs)
                ? sessionTimeoutMs
                : 30000,
            MaxPollIntervalMs = int.TryParse(config["Kafka:MaxPollIntervalMs"], out var maxPollIntervalMs)
                ? maxPollIntervalMs
                : 300000,
            ApiVersionRequestTimeoutMs = int.TryParse(config["Kafka:ApiVersionRequestTimeoutMs"], out var apiVersionRequestTimeoutMs)
                ? apiVersionRequestTimeoutMs
                : 10000,
            ReconnectBackoffMs = int.TryParse(config["Kafka:ReconnectBackoffMs"], out var reconnectBackoffMs)
                ? reconnectBackoffMs
                : 1000,
            ReconnectBackoffMaxMs = int.TryParse(config["Kafka:ReconnectBackoffMaxMs"], out var reconnectBackoffMaxMs)
                ? reconnectBackoffMaxMs
                : 10000
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumer started for key type {KeyType} and message type {MessageType}",
            typeof(TKey).Name, typeof(TMessage).Name);
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

                        // Deserialize message
                        var message = JsonSerializer.Deserialize<KafkaMessage<TMessage>>(result.Message.Value);
                        if (message != null)
                        {
                            _messageStore.AddMessage(message);
                            _logger.LogInformation("Message consumed: {MessageId} with key type {KeyType} and message type {MessageType}",
                                message.Id, typeof(TKey).Name, typeof(TMessage).Name);
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