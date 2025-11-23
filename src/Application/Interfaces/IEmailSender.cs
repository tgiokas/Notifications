using Notifications.Application.Dtos;
namespace Notifications.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(NotificationEmailDto dto, CancellationToken cancellationToken = default);
}
