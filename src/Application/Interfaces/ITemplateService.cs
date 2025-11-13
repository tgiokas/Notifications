using Notifications.Domain.Enums;

namespace Notifications.Application.Interfaces;

public interface ITemplateService
{
    Task<string> RenderAsync(EmailTemplateType type, IDictionary<string, string> tokens);
}
