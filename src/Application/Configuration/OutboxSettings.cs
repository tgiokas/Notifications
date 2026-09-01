using Microsoft.Extensions.Configuration;

namespace Notifications.Application.Configuration;

public class OutboxSettings
{
    // SQLite by default so the outbox mode needs no external database —
    // it's the point of not depending on Kafka either.
    public string ConnectionString { get; set; } = "Data Source=outbox.db";
    public int PollingIntervalMs { get; set; } = 5000;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;

    public static OutboxSettings BindFromConfiguration(IConfiguration configuration)
    {
        var settings = new OutboxSettings();

        var connectionString = configuration["OUTBOX_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(connectionString))
            settings.ConnectionString = connectionString;

        settings.PollingIntervalMs = ParseIntOrDefault(configuration, "OUTBOX_POLLING_INTERVAL_MS", settings.PollingIntervalMs);
        settings.BatchSize = ParseIntOrDefault(configuration, "OUTBOX_BATCH_SIZE", settings.BatchSize);
        settings.MaxAttempts = ParseIntOrDefault(configuration, "OUTBOX_MAX_ATTEMPTS", settings.MaxAttempts);

        return settings;
    }

    private static int ParseIntOrDefault(IConfiguration config, string key, int defaultValue)
    {
        var raw = config[key];
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }
}
