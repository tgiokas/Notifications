// Notifications.Infrastructure/Messaging/KafkaPublisher.cs
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public sealed class KafkaPublisher : IMessagePublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaPublisher> _logger;

    public KafkaPublisher(IConfiguration config, ILogger<KafkaPublisher> logger)
    {
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "kafka:9092",
            Acks = Enum.TryParse(config["Kafka:Acks"], out Acks acks) ? acks : Acks.All,
            RequestTimeoutMs = int.TryParse(config["Kafka:RequestTimeoutMs"], out var rt) ? rt : 5000,
            MessageTimeoutMs = int.TryParse(config["Kafka:MessageTimeoutMs"], out var mt) ? mt : 15000,
            MessageSendMaxRetries = int.TryParse(config["Kafka:MessageSendMaxRetries"], out var mr) ? mr : 5,
            RetryBackoffMs = int.TryParse(config["Kafka:RetryBackoffMs"], out var rb) ? rb : 100,
            ReconnectBackoffMs = int.TryParse(config["Kafka:ReconnectBackoffMs"], out var rcb) ? rcb : 50,
            ReconnectBackoffMaxMs = int.TryParse(config["Kafka:ReconnectBackoffMaxMs"], out var rcbm) ? rcbm : 5000
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublishJsonAsync<T>(
        string route,
        string key,
        T payload,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);

            var msg = new Message<string, string>
            {
                Key = key ?? string.Empty,
                Value = json,
                Headers = new Headers()
            };

            if (headers is not null)
            {
                foreach (var h in headers)
                    msg.Headers!.Add(h.Key, System.Text.Encoding.UTF8.GetBytes(h.Value));
            }

            var result = await _producer.ProduceAsync(route, msg, cancellationToken);
            // Do infra-level logging/metrics here if you need
            _logger.LogDebug("Produced to {TP} (offset {Offset})", result.TopicPartition, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Kafka produce error: {Reason}", ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        try { _producer.Flush(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error flushing Kafka producer during dispose"); }
        finally { _producer.Dispose(); }
    }
}
