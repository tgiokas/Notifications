using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

public interface INotificationChannelPublisher
{
    string Channel { get; } // "email", "sms", "webpush"
    Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default);
}
