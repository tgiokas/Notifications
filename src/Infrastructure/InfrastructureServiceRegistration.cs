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
        // sends mail — used by both delivery modes below (via KafkaEmailConsumer or
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

        // How emails submitted via the REST API get queued for delivery: Kafka (default)
        // or a local outbox, for deployments that don't want a Kafka dependency at all.
        var deliverySettings = DeliverySettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(deliverySettings));

        switch (deliverySettings.EmailMode)
        {
            case EmailDeliveryMode.Outbox:
                var outboxSettings = OutboxSettings.BindFromConfiguration(configuration);
                services.AddSingleton(Options.Create(outboxSettings));

                services.AddDbContext<NotificationsDbContext>(options =>
                    options.UseNpgsql(outboxSettings.ConnectionString));

                services.AddScoped<IOutboxRepository, OutboxRepository>();
                services.AddScoped<IEmailPublisher, OutboxEmailPublisher>();
                services.AddHostedService<OutboxProcessor>();
                break;

            case EmailDeliveryMode.Kafka:
            default:
                var kafkaSettings = KafkaSettings.BindFromConfiguration(configuration);
                services.AddSingleton(Options.Create(kafkaSettings));

                services.AddHostedService<KafkaEmailConsumer>();
                services.AddSingleton<IMessagePublisher, KafkaPublisher>();
                services.AddSingleton<IEmailPublisher, KafkaEmailPublisher>();
                break;
        }

        return services;
    }
}
