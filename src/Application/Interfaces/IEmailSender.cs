using Notifications.Application.Dtos;
using Notifications.Domain.Enums;

namespace Notifications.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(NotificationDto dto, EmailTemplateType emailType, CancellationToken cancellationToken = default);
}
