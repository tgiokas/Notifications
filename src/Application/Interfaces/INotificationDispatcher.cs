using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationRequestDto request, CancellationToken cancellationToken = default);
}