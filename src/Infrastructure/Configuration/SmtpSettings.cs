namespace Notifications.Infrastructure.Configuration;

public class SmtpSettings
{
    public string Host { get; set; } = default!;
    public int Port { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string From { get; set; } = default!;
    public bool UseSsl { get; set; } = true;
}