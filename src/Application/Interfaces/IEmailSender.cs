using Notifications.Application.Dtos;

namespace Notifications.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(NotificationRequestDto dto, CancellationToken cancellationToken = default);
}
