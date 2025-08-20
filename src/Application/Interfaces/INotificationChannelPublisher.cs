// Application/Interfaces/INotificationChannelPublisher.cs
using System.Threading;
using System.Threading.Tasks;
using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface INotificationChannelPublisher
{
    string Channel { get; } // "email", "sms", "webpush"
    Task PublishAsync(NotificationRequestDto request, CancellationToken cancellationToken = default);
}
