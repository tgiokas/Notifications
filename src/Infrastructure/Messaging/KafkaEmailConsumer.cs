using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

public sealed class KafkaEmailConsumer : BackgroundService
{
    private readonly ILogger<KafkaEmailConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;

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

        _topic = config["Kafka:Topic"] ?? "email";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "kafka1:9092,kafka2:9092",
            GroupId = config["Kafka:GroupId"] ?? "ConsumerGroupA",
            // Block & start from earliest if no committed offset
            AutoOffsetReset = Enum.TryParse(config["Kafka:AutoOffsetReset"], out AutoOffsetReset offset) ? offset : AutoOffsetReset.Earliest,
            // We will commit manually after successful processing
            EnableAutoCommit = bool.TryParse(config["Kafka:EnableAutoCommit"], out var autoCommit) ? autoCommit : true,
            AutoCommitIntervalMs = int.TryParse(config["Kafka:AutoCommitIntervalMs"], out var commitInterval) ? commitInterval : 5000,
            // Reasonable stability defaults
            SessionTimeoutMs = int.TryParse(config["Kafka:SessionTimeoutMs"], out var sessionTimeout) ? sessionTimeout : 30000,
            MaxPollIntervalMs = int.TryParse(config["Kafka:MaxPollIntervalMs"], out var maxPoll) ? maxPoll : 300000,
            ApiVersionRequestTimeoutMs = int.TryParse(config["Kafka:ApiVersionRequestTimeoutMs"], out var apiTimeout) ? apiTimeout : 10000,
            // Improve broker/discovery behavior
            ReconnectBackoffMs = int.TryParse(config["Kafka:ReconnectBackoffMs"], out var backoff) ? backoff : 1000,
            ReconnectBackoffMaxMs = int.TryParse(config["Kafka:ReconnectBackoffMaxMs"], out var backoffMax) ? backoffMax : 10000
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
        _logger.LogInformation("KafkaEmailConsumer starting. Topic: {Topic}", _topic);

        try { _consumer.Subscribe(_topic); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to topic {Topic}.", _topic);
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

                        // Commit to avoid poison-pill loops (replace with DLQ publish + commit later)
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
                    // graceful shutdown
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

    private static NotificationRequestDto? ParsePayload(string payload)
    {
        // 1) Envelope with typed Content
        try
        {
            var env = JsonSerializer.Deserialize<KafkaMessage<NotificationRequestDto>>(payload, JsonOpts);
            if (env?.Content is not null) return env.Content;
        }
        catch { /* fall through */ }

        // 2) Envelope with string Content (legacy)
        try
        {
            var envRaw = JsonSerializer.Deserialize<KafkaMessage<string>>(payload, JsonOpts);
            if (!string.IsNullOrWhiteSpace(envRaw?.Content))
                return JsonSerializer.Deserialize<NotificationRequestDto>(envRaw.Content, JsonOpts);
        }
        catch { /* fall through */ }

        // 3) Bare DTO
        try
        {
            return JsonSerializer.Deserialize<NotificationRequestDto>(payload, JsonOpts);
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
