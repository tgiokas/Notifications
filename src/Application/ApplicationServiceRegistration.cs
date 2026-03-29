using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;

namespace Notifications.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<ISendGridEventHandler, SendGridEventHandler>();

        return services;
    }
}
