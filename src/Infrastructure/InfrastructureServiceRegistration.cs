using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Interfaces;
using Notifications.Infrastructure.ExternalServices;
using Notifications.Infrastructure.Messaging;

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

        // Bind KafkaSettings from env variables
        var kafkaSettings = KafkaSettings.BindFromConfiguration(configuration);
        services.AddSingleton(Options.Create(kafkaSettings));

        // HttpClient for StorageService 
        services.AddHttpClient<IStorageApiClient, StorageApiClient>(client =>
        {
            client.BaseAddress = new Uri(attachmentSettings.StorageBaseUrl); 
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Attachment resolver (downloads refs + enforces size cap)
        services.AddScoped<IAttachmentResolver, AttachmentResolver>();

        // Register the concrete email provider
        switch (emailSettings.Provider)
        {
            case EmailProviderType.SendGrid:
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
