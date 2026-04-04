using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Notifications.Application.Interfaces;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Infrastructure.Messaging;

namespace Notifications.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, string databaseProvider)
    {
        // Register the concrete sender / email provider
        var provider = configuration["EMAIL_PROVIDER"]?.Trim().ToLowerInvariant() ?? "smtp";

        switch (provider)
        {
            case "sendgrid":
                services.AddScoped<IEmailSender, SendGridEmailSender>();
                break;
            default:
                services.AddScoped<IEmailSender, SmtpEmailSender>();
                break;
        }

        // Add Kafka Consumer
        services.AddHostedService<KafkaEmailConsumer>();

        return services;
    }
}
