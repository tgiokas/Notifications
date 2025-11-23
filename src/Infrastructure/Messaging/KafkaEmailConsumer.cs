using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Confluent.Kafka;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.Messaging;

public sealed class KafkaEmailConsumer : BackgroundService
{
    private readonly ILogger<KafkaEmailConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
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

        _topics = config.GetSection("Kafka:Topics").Get<string[]>()
           ?? (config["Kafka:Topic"] ?? "email")
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "kafka1:9092",

            // Base delay before reconnecting to a broker
            ReconnectBackoffMs = int.TryParse(config["Kafka:ReconnectBackoffMs"], out var reconnectBackoffMs) ? reconnectBackoffMs : 50,
            // Maximum delay when exponential backoff applies
            ReconnectBackoffMaxMs = int.TryParse(config["Kafka:ReconnectBackoffMaxMs"], out var reconnectBackoffMaxMs) ? reconnectBackoffMaxMs : 5000,

            // Consumer group for load balancing
            GroupId = config["Kafka:GroupId"] ?? "email-consumers",
            
            // Block & start from earliest if no committed offset
            AutoOffsetReset = Enum.TryParse(config["Kafka:AutoOffsetReset"], out AutoOffsetReset offset) ? offset : AutoOffsetReset.Earliest,
            
            // commit manually after successful processing
            EnableAutoCommit = bool.TryParse(config["Kafka:EnableAutoCommit"], out var enableAutoCommit) ? enableAutoCommit : true,
            AutoCommitIntervalMs = int.TryParse(config["Kafka:AutoCommitIntervalMs"], out var commitInterval) ? commitInterval : 5000,

            // Heartbeat timeout before rebalance
            SessionTimeoutMs = int.TryParse(config["Kafka:SessionTimeoutMs"], out var sessionTimeout) ? sessionTimeout : 30000,
            
            // Max allowed processing time before consumer removed
            MaxPollIntervalMs = int.TryParse(config["Kafka:MaxPollIntervalMs"], out var maxPoll) ? maxPoll : 300000,

            // Timeout for metadata / version requests
            ApiVersionRequestTimeoutMs = int.TryParse(config["Kafka:ApiVersionRequestTimeoutMs"], out var apiTimeout) ? apiTimeout : 10000,
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

                    var dto = ParsePayload(result.Message.Value);
                    if (dto == null)
                    {
                        _logger.LogWarning("Skipping message at {TPO}: unable to parse payload.", result.TopicPartitionOffset);

                        // Commit to avoid  loops
                        _consumer.Commit(result);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                    await sender.SendAsync(dto, stoppingToken);

                    _consumer.Commit(result); // success
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
