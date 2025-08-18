// Publisher: EmailKafkaPublisher.cs
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using System.Text.Json;

namespace Notifications.Infrastructure.Messaging;

public class KafkaEmailPublisher1 : IDisposable
{
    private readonly IProducer<Ignore, string> _producer;
    private readonly ILogger<KafkaEmailPublisher1> _logger;

    public KafkaEmailPublisher1(IConfiguration config, ILogger<KafkaEmailPublisher1> logger)
    {
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"],
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<Ignore, string>(producerConfig).Build();
    }

    public async Task PublishAsync(string topic, NotificationRequestDto email, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(email);

        var result = await _producer.ProduceAsync(topic, new Message<Ignore, string>
        {
            Key = default,
            Value = payload
        }, cancellationToken);

        _logger.LogInformation("Email message sent to {TopicPartitionOffset}", result.TopicPartitionOffset);
    }

    public void Dispose() => _producer.Dispose();
}
