using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface INotificationDispatcher
{
    Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default);
}