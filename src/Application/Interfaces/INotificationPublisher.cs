using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default);
}