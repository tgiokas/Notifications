using Microsoft.Extensions.Configuration;
using Confluent.Kafka;

namespace Notifications.Application.Configuration;

public class KafkaSettings
{
    // Bootstrap servers
    public string BootstrapServers { get; set; } = string.Empty;

    // Topics for the Consumer to subscribe to
    public string[] Topics { get; set; } = Array.Empty<string>();
    
    // Consumer group for load balancing
    public string GroupId { get; set; } = string.Empty;
    
    // Base delay before reconnecting to a broker
    public int ReconnectBackoffMs { get; set; }
   
    // Maximum delay when exponential backoff applies
    public int ReconnectBackoffMaxMs { get; set; }

    // Offset reset strategy
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;

    // Auto-commit strategy
    public bool EnableAutoCommit { get; set; } = false;
    public int AutoCommitIntervalMs { get; set; } = 5000;
    
    // Heartbeat timeout before rebalance
    public int SessionTimeoutMs { get; set; } = 30000;
    
    // Max processing time before Kafka considers the consumer dead
    public int MaxPollIntervalMs { get; set; } = 300000;
    
    // Timeout for metadata / version requests
    public int ApiVersionRequestTimeoutMs { get; set; } = 10000;

    public static KafkaSettings BindFromConfiguration(IConfiguration configuration)
    {
        var topicsRaw = configuration["NOTIFY_KAFKA_TOPICS"]
            ?? throw new ArgumentNullException(nameof(configuration), "NOTIFY_KAFKA_TOPICS is not set.");

        var topics = topicsRaw
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (topics.Length == 0)
            throw new ArgumentException("NOTIFY_KAFKA_TOPICS must contain at least one topic.", nameof(configuration));

        return new KafkaSettings
        {
            BootstrapServers = configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? throw new ArgumentNullException(nameof(configuration), "KAFKA_BOOTSTRAP_SERVERS is not set."),
            Topics = topics,

            GroupId = configuration["NOTIFY_KAFKA_GROUP_ID"]
                ?? throw new ArgumentNullException(nameof(configuration), "NOTIFY_KAFKA_GROUP_ID is not set."),
            ReconnectBackoffMs = ParseInt(configuration, "NOTIFY_KAFKA_RECONNECT_BACKOFF_MS"),
            ReconnectBackoffMaxMs = ParseInt(configuration, "NOTIFY_KAFKA_RECONNECT_BACKOFF_MAX_MS"),
            AutoOffsetReset = Enum.TryParse(configuration["NOTIFY_KAFKA_AUTO_OFFSET_RESET"], true, out AutoOffsetReset offset)
                ? offset
                : throw new ArgumentException("NOTIFY_KAFKA_AUTO_OFFSET_RESET is not a valid value.", nameof(configuration)),
            EnableAutoCommit = ParseBool(configuration, "NOTIFY_KAFKA_ENABLE_AUTO_COMMIT"),
            AutoCommitIntervalMs = ParseInt(configuration, "NOTIFY_KAFKA_AUTO_COMMIT_INTERVAL_MS"),
            SessionTimeoutMs = ParseInt(configuration, "NOTIFY_KAFKA_SESSION_TIMEOUT_MS"),
            MaxPollIntervalMs = ParseInt(configuration, "NOTIFY_KAFKA_MAX_POLL_INTERVAL_MS"),
            ApiVersionRequestTimeoutMs = ParseInt(configuration, "NOTIFY_KAFKA_API_VERSION_REQUEST_TIMEOUT_MS")
        };
    }

    private static int ParseInt(IConfiguration config, string key)
    {
        var raw = config[key]
            ?? throw new ArgumentNullException(nameof(config), $"{key} is not set.");
        if (!int.TryParse(raw, out var value))
            throw new ArgumentException($"{key} is not a valid integer.", nameof(config));
        return value;
    }

    private static bool ParseBool(IConfiguration config, string key)
    {
        var raw = config[key]
            ?? throw new ArgumentNullException(nameof(config), $"{key} is not set.");
        if (!bool.TryParse(raw, out var value))
            throw new ArgumentException($"{key} is not a valid boolean.", nameof(config));
        return value;
    }
}