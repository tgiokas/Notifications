using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;
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
        // Bind EmailSettings from env variables
        var emailSettings = EmailSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(emailSettings));

        // Bind AttachmentSettings from env variables
        var attachmentSettings = AttachmentSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(attachmentSettings));

        // HttpClient for StorageService
        services.AddHttpClient<IStorageApiClient, StorageApiClient>(client =>
        {
            client.BaseAddress = new Uri(attachmentSettings.StorageBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Attachment resolver (downloads refs + enforces size cap)
        services.AddScoped<IAttachmentResolver, AttachmentResolver>();

        // Register the concrete email provider. This is the only thing that actually sends mail
        // used by both KafkaEmailConsumer and OutboxProcessor).
        switch (emailSettings.Provider)
        {
            case EmailProviderType.SendGrid:
                services.AddScoped<IEmailSender, SendGridEmailSender>();
                break;
            default:
                services.AddScoped<IEmailSender, SmtpEmailSender>();
                break;
        }

        // KAFKA_ENABLED picks one of two mutually exclusive deployment profiles:
        //   true  -> KafkaEmailConsumer only. No Postgres/outbox is configured at
        //            all, matching an environment that has Kafka but no database.
        //            The REST email endpoint is disabled (see DisabledEmailPublisher).
        //   false -> Outbox only (EmailsController -> EmailService -> OutboxProcessor
        //            -> IEmailSender). No Kafka settings are required.
        // Defaults to true to preserve original behavior for existing deployments.
        var kafkaEnabledRaw = configuration["KAFKA_ENABLED"];
        var kafkaEnabled = string.IsNullOrWhiteSpace(kafkaEnabledRaw)
            ? true
            : bool.TryParse(kafkaEnabledRaw, out var parsed)
                ? parsed
                : throw new ArgumentException($"Invalid KAFKA_ENABLED value: '{kafkaEnabledRaw}'. Expected 'true' or 'false'.");

        if (kafkaEnabled)
        {
            var kafkaSettings = KafkaSettings.BindFromConfiguration(configuration);
            services.AddSingleton(Options.Create(kafkaSettings));
            services.AddHostedService<KafkaEmailConsumer>();

            // No outbox/Postgres in this profile — the REST endpoint has nothing to
            // queue into. Still register IEmailPublisher so DI resolves cleanly; it
            // just reports itself as disabled when called.
            services.AddSingleton<IEmailPublisher, DisabledEmailPublisher>();
        }
        else
        {
            var outboxSettings = OutboxSettings.BindFromConfiguration(configuration);
            services.AddSingleton(Options.Create(outboxSettings));

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(outboxSettings.ConnectionString));

            services.AddScoped<IOutboxRepository, OutboxRepository>();
            services.AddScoped<IEmailPublisher, OutboxEmailPublisher>();
            services.AddHostedService<OutboxProcessor>();
        }

        return services;
    }
}
