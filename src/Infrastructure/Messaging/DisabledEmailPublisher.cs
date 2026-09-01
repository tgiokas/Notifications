using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

/// Registered instead of OutboxEmailPublisher when KAFKA_ENABLED=true: that
/// deployment profile has no Postgres/outbox configured, so the REST email
/// endpoint has nothing to queue into. Throwing here (rather than leaving
/// IEmailPublisher unregistered) keeps the DI graph valid and lets
/// EmailService turn this into a clear, typed failure instead of a raw
/// "service not found" exception.
public class DisabledEmailPublisher : IEmailPublisher
{
    public Task PublishAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "The REST email endpoint is disabled in this deployment: KAFKA_ENABLED=true, " +
            "so no outbox/Postgres is configured. Set KAFKA_ENABLED=false to enable it.");
    }
}
