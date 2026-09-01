using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

/// Publishes an email onto the outbox so it is durably queued and later sent
/// by OutboxProcessor via IEmailSender, instead of being sent inline from the
/// API call. Unrelated to Kafka: KafkaEmailConsumer is a separate, always-on
/// inbound path fed by external producers, not by this interface.
public interface IEmailPublisher
{
    Task PublishAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default);
}
