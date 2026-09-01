using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;

namespace Notifications.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ISendGridEventHandler, SendGridEventHandler>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<ITemplateService, TemplateService>();
        return services;
    }
}
