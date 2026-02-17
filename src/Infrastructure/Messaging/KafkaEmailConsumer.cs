using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Confluent.Kafka;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public sealed class KafkaEmailConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KafkaEmailConsumer> _logger;
    private readonly string[] _topics;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KafkaEmailConsumer(IConfiguration config,
                              ILogger<KafkaEmailConsumer> logger,
                              IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = config;

        // Read topics from .env
        var topicsRaw = _configuration["NOTIFY_KAFKA_TOPICS"]
            ?? throw new ArgumentNullException(nameof(_configuration), "NOTIFY_KAFKA_TOPICS is not set.");
        _topics = topicsRaw
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (_topics.Length == 0)
            throw new ArgumentException("KAFKA_TOPICS must contain at least one topic.", nameof(_configuration));

        var consumerConfig = new ConsumerConfig
        {
            // Bootstrap servers
            BootstrapServers = _configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? throw new ArgumentNullException(nameof(_configuration), "KAFKA_BOOTSTRAP_SERVERS is not set."),
            
            // Base delay before reconnecting to a broker
            ReconnectBackoffMs =
                int.TryParse(_configuration["NOTIFY_KAFKA_RECONNECT_BACKOFF_MS"], out var reconnectBackoffMs)
                    ? reconnectBackoffMs
                    : throw new ArgumentException("NOTIFY_KAFKA_RECONNECT_BACKOFF_MS is not a valid integer.", nameof(_configuration)),

            // Maximum delay when exponential backoff applies
            ReconnectBackoffMaxMs =
                int.TryParse(_configuration["NOTIFY_KAFKA_RECONNECT_BACKOFF_MAX_MS"], out var reconnectBackoffMaxMs)
                    ? reconnectBackoffMaxMs
                    : throw new ArgumentException("NOTIFY_KAFKA_RECONNECT_BACKOFF_MAX_MS is not a valid integer.", nameof(_configuration)),

            // Consumer group for load balancing
            GroupId = _configuration["NOTIFY_KAFKA_GROUP_ID"]
                ?? throw new ArgumentNullException(nameof(_configuration), "NOTIFY_KAFKA_GROUP_ID is not set."),

            // Offset reset strategy
            AutoOffsetReset =
                Enum.TryParse(_configuration["NOTIFY_KAFKA_AUTO_OFFSET_RESET"], true, out AutoOffsetReset offset)
                    ? offset
                    : throw new ArgumentException("NOTIFY_KAFKA_AUTO_OFFSET_RESET is not a valid value.", nameof(_configuration)),

            // Auto-commit strategy
            EnableAutoCommit =
                bool.TryParse(_configuration["NOTIFY_KAFKA_ENABLE_AUTO_COMMIT"], out var autoCommit)
                    ? autoCommit
                    : throw new ArgumentException("NOTIFY_KAFKA_ENABLE_AUTO_COMMIT is not a valid boolean.", nameof(_configuration)),

            AutoCommitIntervalMs =
                int.TryParse(_configuration["NOTIFY_KAFKA_AUTO_COMMIT_INTERVAL_MS"], out var commitInterval)
                    ? commitInterval
                    : throw new ArgumentException("NOTIFY_KAFKA_AUTO_COMMIT_INTERVAL_MS is not a valid integer.", nameof(_configuration)),

            // Heartbeat timeout before rebalance
            SessionTimeoutMs =
                int.TryParse(_configuration["NOTIFY_KAFKA_SESSION_TIMEOUT_MS"], out var sessionTimeout)
                    ? sessionTimeout
                    : throw new ArgumentException("NOTIFY_KAFKA_SESSION_TIMEOUT_MS is not a valid integer.", nameof(_configuration)),

            // Max processing time before Kafka considers the consumer dead
            MaxPollIntervalMs =
                int.TryParse(_configuration["NOTIFY_KAFKA_MAX_POLL_INTERVAL_MS"], out var maxPoll)
                    ? maxPoll
                    : throw new ArgumentException("NOTIFY_KAFKA_MAX_POLL_INTERVAL_MS is not a valid integer.", nameof(_configuration)),

            // Timeout for metadata / version requests
            ApiVersionRequestTimeoutMs =
                int.TryParse(_configuration["NOTIFY_KAFKA_API_VERSION_REQUEST_TIMEOUT_MS"], out var apiTimeout)
                    ? apiTimeout
                    : throw new ArgumentException("NOTIFY_KAFKA_API_VERSION_REQUEST_TIMEOUT_MS is not a valid integer.", nameof(_configuration)),
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                    _logger.LogError("Fatal Kafka error: {Reason}", e.Reason);
                else
                    _logger.LogWarning("Kafka error: {Reason}", e.Reason);
            })
            .SetLogHandler((_, msg) =>
            {
                _logger.LogDebug("Kafka[{Name}] {Level}: {Message}", msg.Name, msg.Level, msg.Message);
            })
            .Build();
    }


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaEmailConsumer starting. Topic: {Topic}", _topics);

        try
        {
            _consumer.Subscribe(_topics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to topic {Topic}.", _topics);
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = _consumer.Consume(stoppingToken);
                    if (result == null) continue;
                    
                    _logger.LogInformation("Message from topic {Topic} consumed", result.Topic);
                    
                    var emailDto = ParsePayload(result.Message.Value);
                    if (emailDto == null)
                    {
                        _logger.LogWarning("Skipping message at {TPO}: unable to parse payload.", result.TopicPartitionOffset);

                        // Commit to avoid  loops
                        _consumer.Commit(result);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                    await sender.SendAsync(emailDto, stoppingToken);

                    _consumer.Commit(result); // success
                    
                    _logger.LogInformation("Offset commited at {TPO} ", result.TopicPartitionOffset);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "ConsumeException at {TPO}: {Reason}",
                        result?.TopicPartitionOffset, ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "JSON error at {TPO}; committing to skip poison message.",
                        result?.TopicPartitionOffset);
                    if (result is not null) _consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled processing error at {TPO}. Backing off briefly.",
                        result?.TopicPartitionOffset);
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            try { _consumer.Close(); } // leave group & commit last offsets
            catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
        }
    }

    private static NotificationEmailDto? ParsePayload(string payload)
    {
        // 1) Envelope with typed Content
        try
        {
            var env = JsonSerializer.Deserialize<KafkaMessage<NotificationEmailDto>>(payload, JsonOpts);
            if (env?.Content is not null) return env.Content;
        }
        catch { /* fall through */ }

        // 2) Envelope with string Content
        try
        {
            var envRaw = JsonSerializer.Deserialize<KafkaMessage<string>>(payload, JsonOpts);
            if (!string.IsNullOrWhiteSpace(envRaw?.Content))
                return JsonSerializer.Deserialize<NotificationEmailDto>(envRaw.Content, JsonOpts);
        }
        catch { /* fall through */ }

        // 3) Bare DTO
        try
        {
            return JsonSerializer.Deserialize<NotificationEmailDto>(payload, JsonOpts);
        }
        catch { }

        return null;
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
