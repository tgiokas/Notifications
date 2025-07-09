using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using System.Text.Json;

namespace Notifications.Infrastructure.Messaging;

public class KafkaProducerService<TKey, TMessage> : IDisposable
    where TMessage : class
    where TKey : class
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaProducerService<TKey, TMessage>> _logger;

    public KafkaProducerService(IConfiguration config, ILogger<KafkaProducerService<TKey, TMessage>> logger)
    {
        _logger = logger;
        _topic = config["Kafka:Topic"] ?? "DefaultTopic";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],

            // Critical settings for better failover handling
            Acks = Enum.Parse<Acks>(config["Kafka:Acks"] ?? "Leader"),

            // Improve broker discovery and failover
            SocketConnectionSetupTimeoutMs = int.TryParse(config["Kafka:SocketConnectionSetupTimeoutMs"], out var socketConnectionSetupTimeoutMs) ? socketConnectionSetupTimeoutMs : 10000,
            SocketTimeoutMs = int.TryParse(config["Kafka:SocketTimeoutMs"], out var socketTimeoutMs) ? socketTimeoutMs : 5000,

            // Retry settings
            MessageSendMaxRetries = int.TryParse(config["Kafka:MessageSendMaxRetries"], out var messageSendMaxRetries) ? messageSendMaxRetries : 5,
            RetryBackoffMs = int.TryParse(config["Kafka:RetryBackoffMs"], out var retryBackoffMs) ? retryBackoffMs : 100,
            ReconnectBackoffMs = int.TryParse(config["Kafka:ReconnectBackoffMs"], out var reconnectBackoffMs) ? reconnectBackoffMs : 50,
            ReconnectBackoffMaxMs = int.TryParse(config["Kafka:ReconnectBackoffMaxMs"], out var reconnectBackoffMaxMs) ? reconnectBackoffMaxMs : 5000,

            // Reasonable timeouts
            RequestTimeoutMs = int.TryParse(config["Kafka:RequestTimeoutMs"], out var requestTimeoutMs) ? requestTimeoutMs : 5000,
            MessageTimeoutMs = int.TryParse(config["Kafka:MessageTimeoutMs"], out var messageTimeoutMs) ? messageTimeoutMs : 15000
        };

        _logger.LogInformation("Configuring Kafka producer with servers: {servers}",
            producerConfig.BootstrapServers);

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task<DeliveryResult<string, string>> ProduceMessageAsync(TKey? key, KafkaMessage<TMessage> message)
    {
        try
        {
            string? serializedKey = key != null ? SerializeKey(key) : null;
            string serializedMessage = JsonSerializer.Serialize(message);

            _logger.LogDebug("Attempting to send message {id} to topic {topic}",
                message.Id, _topic);

            var result = await _producer.ProduceAsync(_topic,
                new Message<string, string> 
                {  Key = serializedKey?? string.Empty,
                   Value = serializedMessage 
                }
             );

            _logger.LogInformation("Message sent: {MessageId} to {TopicPartitionOffset}",
                message.Id, result.TopicPartitionOffset);

            return result;
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Error producing message {id}: {Reason}",
                message.Id, ex.Error.Reason);
            throw;
        }
    }

    private string SerializeKey(TKey key)
    {
        return key switch
        {
            string str => str,
            int i => i.ToString(),
            long l => l.ToString(),
            Guid guid => guid.ToString(),
            _ => JsonSerializer.Serialize(key)
        };
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error flushing producer on dispose");
        }
        finally
        {
            _producer.Dispose();
        }
    }
}