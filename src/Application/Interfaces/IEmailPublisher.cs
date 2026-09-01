using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

/// Publishes an email onto the async delivery pipeline (Kafka) so it is picked
/// up and sent by the same KafkaEmailConsumer / IEmailSender path used by
/// every other producer, instead of being sent inline from the API call.
public interface IEmailPublisher
{
    Task PublishAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default);
}
