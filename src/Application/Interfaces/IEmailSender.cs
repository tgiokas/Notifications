using Notifications.Application.Dtos;
namespace Notifications.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default);
}
