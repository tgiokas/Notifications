using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Interfaces;
using Notifications.Application.Services;
using Notifications.Domain.Interfaces;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Infrastructure.Messaging;
using Notifications.Infrastructure.Persistence;
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

        // Register the concrete email provider. This is the only thing that actually
        // sends mail — used by both inbound paths below (KafkaEmailConsumer and
        // OutboxProcessor), never called directly from the REST controller.
        switch (emailSettings.Provider)
        {
            case EmailProviderType.SendGrid:
                services.AddScoped<IEmailSender, SendGridEmailSender>();
                break;
            default:
                services.AddScoped<IEmailSender, SmtpEmailSender>();
                break;
        }

        // Kafka consumer: unrelated to the REST API. Consumes whatever external
        // producers (e.g. the Authentication service) publish onto these topics
        // and delivers via IEmailSender, same as before the REST endpoint existed.
        var kafkaSettings = KafkaSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(kafkaSettings));
        services.AddHostedService<KafkaEmailConsumer>();

        // Outbox: the REST email endpoint's only delivery path. EmailsController ->
        // EmailService -> OutboxEmailPublisher writes a row; OutboxProcessor polls it
        // and delivers via the same IEmailSender above. No Kafka involved.
        var outboxSettings = OutboxSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(outboxSettings));

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(outboxSettings.ConnectionString));

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IEmailPublisher, OutboxEmailPublisher>();
        services.AddHostedService<OutboxProcessor>();

        return services;
    }
}
