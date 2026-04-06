using Microsoft.Extensions.Configuration;

namespace Notifications.Application.Configuration;

public enum EmailProviderType
{
    Smtp = 1,
    SendGrid = 2
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
}

public class SendGridSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Notifications";
}

public class EmailSettings
{
    public EmailProviderType Provider { get; set; } = EmailProviderType.Smtp;
    public SmtpSettings Smtp { get; set; } = new();
    public SendGridSettings SendGrid { get; set; } = new();

    /// Binds EmailSettings from flat environment variables.
    public static EmailSettings BindFromConfiguration(IConfiguration configuration)
    {
        var settings = new EmailSettings();

        // Provider
        var providerStr = configuration["EMAIL_PROVIDER"]?.Trim().ToLowerInvariant() ?? "smtp";

        settings.Provider = providerStr switch
        {
            "sendgrid" => EmailProviderType.SendGrid,
            _ => EmailProviderType.Smtp
        };

        // Validate and bind provider-specific settings
        switch (settings.Provider)
        {
            case EmailProviderType.Smtp:
                settings.Smtp = BindSmtpSettings(configuration);
                break;

            case EmailProviderType.SendGrid:
                settings.SendGrid = BindSendGridSettings(configuration);
                break;
        }

        return settings;
    }

    private static SmtpSettings BindSmtpSettings(IConfiguration configuration)
    {
        var portStr = configuration["SMTP_PORT"]
            ?? throw new ArgumentNullException(nameof(configuration), "SMTP_PORT is not set.");

        if (!int.TryParse(portStr, out var port))
            throw new ArgumentException($"Invalid SMTP_PORT value: '{portStr}'. Expected a valid integer.");

        return new SmtpSettings
        {
            Host = configuration["SMTP_HOST"]
                ?? throw new ArgumentNullException(nameof(configuration), "SMTP_HOST is not set."),

            Port = port,

            Username = configuration["SMTP_USERNAME"]
                ?? throw new ArgumentNullException(nameof(configuration), "SMTP_USERNAME is not set."),

            Password = configuration["SMTP_PASSWORD"]
                ?? throw new ArgumentNullException(nameof(configuration), "SMTP_PASSWORD is not set."),

            From = configuration["SMTP_FROM"]
                ?? throw new ArgumentNullException(nameof(configuration), "SMTP_FROM is not set.")
        };
    }

    private static SendGridSettings BindSendGridSettings(IConfiguration configuration)
    {
        return new SendGridSettings
        {
            ApiKey = configuration["SENDGRID_API_KEY"]
                ?? throw new ArgumentNullException(nameof(configuration), "SENDGRID_API_KEY is not set."),

            FromEmail = configuration["SENDGRID_FROM_EMAIL"]
                ?? throw new ArgumentNullException(nameof(configuration), "SENDGRID_FROM_EMAIL is not set."),

            FromName = configuration["SENDGRID_FROM_NAME"] ?? "Notifications"
        };
    }
}