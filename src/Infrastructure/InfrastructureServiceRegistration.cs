using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Interfaces;
using Notifications.Domain.Interfaces;
using Notifications.Infrastructure.Database;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Infrastructure.Messaging;
using Notifications.Infrastructure.Repositories;

namespace Notifications.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, string databaseProvider)
    {        
        var connectionString = configuration["NOTIFY_DB_CONNECTION"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Database connection string 'NOTIFY_DB_CONNECTION' is not configured.");

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {          
            switch (databaseProvider.ToLower())
            {
                case "sqlserver":
                    options.UseSqlServer(connectionString);
                    break;

                case "postgresql":
                    options.UseNpgsql(connectionString);
                    break;

                case "sqlite":
                    //options.UseSqlite(connectionString);
                    break;

                default:
                    throw new ArgumentException($"Unsupported database provider: {databaseProvider}");
            }
        });

        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();

        services.AddScoped<IEmailSender, EmailSenderFactory>();

        // Seed filesystem templates into DB on first run
        services.AddHostedService<EmailTemplateSeedService>();

        // Add Kafka Consumer
        services.AddHostedService<KafkaEmailConsumer>();

        return services;
    }
}
