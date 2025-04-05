using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(NotificationRequestDto dto, CancellationToken cancellationToken = default);
}
