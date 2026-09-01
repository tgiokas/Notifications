using Microsoft.Extensions.Configuration;

namespace Notifications.Application.Configuration;

public enum EmailDeliveryMode
{
    Kafka = 1,
    Outbox = 2
}

/// Chooses how emails submitted via the REST API are queued for delivery:
/// Kafka (default, published onto a topic and picked up by KafkaEmailConsumer)
/// or Outbox (written to a local durable store and picked up by OutboxDispatcher),
/// for deployments that don't want a Kafka dependency at all.
public class DeliverySettings
{
    public EmailDeliveryMode EmailMode { get; set; } = EmailDeliveryMode.Kafka;

    public static DeliverySettings BindFromConfiguration(IConfiguration configuration)
    {
        var raw = configuration["EMAIL_DELIVERY_MODE"]?.Trim().ToLowerInvariant() ?? "kafka";

        return new DeliverySettings
        {
            EmailMode = raw switch
            {
                "outbox" => EmailDeliveryMode.Outbox,
                "kafka" => EmailDeliveryMode.Kafka,
                _ => throw new ArgumentException($"Invalid EMAIL_DELIVERY_MODE value: '{raw}'. Expected 'kafka' or 'outbox'.")
            }
        };
    }
}
