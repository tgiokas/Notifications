using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationRequestDto request, CancellationToken cancellationToken = default);
}