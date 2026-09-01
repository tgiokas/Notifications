using Microsoft.Extensions.Configuration;
using Confluent.Kafka;

namespace Notifications.Application.Configuration;

public class KafkaSettings
{
    // Bootstrap servers
    public string BootstrapServers { get; set; } = string.Empty;
    
    // Base delay before reconnecting to a broker
    public int ReconnectBackoffMs { get; set; }
   
    // Maximum delay when exponential backoff applies
    public int ReconnectBackoffMaxMs { get; set; }

    // Time allowed to establish initial TCP connection
    public int SocketConnectionSetupTimeoutMs { get; set; }

    // How long to wait for socket operations before failing
    public int SocketTimeoutMs { get; set; }


    // Topics for the Consumer to subscribe to
    public string[] Topics { get; set; } = Array.Empty<string>();

    // Consumer group for load balancing
    public string GroupId { get; set; } = string.Empty;

    // Offset reset strategy
    public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Earliest;
    
    // Heartbeat timeout before rebalance
    public int SessionTimeoutMs { get; set; }
    
    // Max processing time before Kafka considers the consumer dead
    public int MaxPollIntervalMs { get; set; }

    // SASL / TLS — opt-in only; when absent the consumer runs plaintext (default behaviour unchanged)
    public string? SecurityProtocol { get; set; }
    public string? SaslMechanism { get; set; }
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }

    // Producer settings — optional, used only by IMessagePublisher (e.g. the REST email endpoint).
    // Defaults to the first entry of Topics so a message published here lands on a topic
    // KafkaEmailConsumer already subscribes to.
    public string ProduceTopic { get; set; } = string.Empty;
    public int RetryBackoffMs { get; set; } = 100;
    public int RequestTimeoutMs { get; set; } = 30000;
    public int MessageTimeoutMs { get; set; } = 30000;

    public static KafkaSettings BindFromConfiguration(IConfiguration configuration)
    {
        var topicsRaw = configuration["NOTIFY_KAFKA_TOPICS"]
            ?? throw new ArgumentNullException(nameof(configuration), "NOTIFY_KAFKA_TOPICS is not set.");

        var topics = topicsRaw
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (topics.Length == 0)
            throw new ArgumentException("NOTIFY_KAFKA_TOPICS must contain at least one topic.", nameof(configuration));

        var settings = new KafkaSettings
        {
            // Broker connection settings
            BootstrapServers = configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? throw new ArgumentNullException(nameof(configuration), "KAFKA_BOOTSTRAP_SERVERS is not set."),
            ReconnectBackoffMs = ParseInt(configuration, "NOTIFY_KAFKA_RECONNECT_BACKOFF_MS"),
            ReconnectBackoffMaxMs = ParseInt(configuration, "NOTIFY_KAFKA_RECONNECT_BACKOFF_MAX_MS"),
            SocketConnectionSetupTimeoutMs = ParseInt(configuration, "NOTIFY_KAFKA_SOCKET_CONNECTION_SETUP_TIMEOUT_MS"),
            SocketTimeoutMs = ParseInt(configuration, "NOTIFY_KAFKA_SOCKET_TIMEOUT_MS"),

            // Consumer settings
            Topics = topics,
            GroupId = configuration["NOTIFY_KAFKA_GROUP_ID"]
                ?? throw new ArgumentNullException(nameof(configuration), "NOTIFY_KAFKA_GROUP_ID is not set."),
            AutoOffsetReset = Enum.TryParse(configuration["NOTIFY_KAFKA_AUTO_OFFSET_RESET"], true, out AutoOffsetReset offset)
                ? offset
                : throw new ArgumentException("NOTIFY_KAFKA_AUTO_OFFSET_RESET is not a valid value.", nameof(configuration)),
            SessionTimeoutMs = ParseInt(configuration, "NOTIFY_KAFKA_SESSION_TIMEOUT_MS"),
            MaxPollIntervalMs = ParseInt(configuration, "NOTIFY_KAFKA_MAX_POLL_INTERVAL_MS"),
        };

        // SASL / TLS — opt-in; missing or blank → plaintext default unchanged
        settings.SecurityProtocol = configuration["NOTIFY_KAFKA_SECURITY_PROTOCOL"];
        settings.SaslMechanism    = configuration["NOTIFY_KAFKA_SASL_MECHANISM"];
        settings.SaslUsername     = configuration["NOTIFY_KAFKA_SASL_USERNAME"];
        settings.SaslPassword     = configuration["NOTIFY_KAFKA_SASL_PASSWORD"];

        // Producer settings — optional; fall back to sane defaults / the first consumed topic
        // so existing deployments don't need new env vars to keep working.
        settings.ProduceTopic = string.IsNullOrWhiteSpace(configuration["NOTIFY_KAFKA_PRODUCE_TOPIC"])
            ? topics[0]
            : configuration["NOTIFY_KAFKA_PRODUCE_TOPIC"]!;
        settings.RetryBackoffMs = ParseIntOrDefault(configuration, "NOTIFY_KAFKA_RETRY_BACKOFF_MS", settings.RetryBackoffMs);
        settings.RequestTimeoutMs = ParseIntOrDefault(configuration, "NOTIFY_KAFKA_REQUEST_TIMEOUT_MS", settings.RequestTimeoutMs);
        settings.MessageTimeoutMs = ParseIntOrDefault(configuration, "NOTIFY_KAFKA_MESSAGE_TIMEOUT_MS", settings.MessageTimeoutMs);

        return settings;
    }

    private static int ParseIntOrDefault(IConfiguration config, string key, int defaultValue)
    {
        var raw = config[key];
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        return int.TryParse(raw, out var value) ? value : defaultValue;
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