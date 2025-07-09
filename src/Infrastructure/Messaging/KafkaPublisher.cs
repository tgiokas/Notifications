using Microsoft.Extensions.Options;

using Confluent.Kafka;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.Configuration;

namespace Notifications.Infrastructure.Messaging;


public class KafkaPublisher : IKafkaPublisher, IDisposable
{
    private readonly IProducer<Null, string> _producer;

    public KafkaPublisher(IOptions<KafkaSettings> kafkaSettings)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = kafkaSettings.Value.BootstrapServers
        };
        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task PublishMessageAsync(string topic, string message, CancellationToken cancellationToken = default)
    {
        await _producer.ProduceAsync(topic, new Message<Null, string> { Value = message }, cancellationToken);
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
