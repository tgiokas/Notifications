#nullable enable
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Messaging;

/// <summary>
/// Email consumer with Idempotency + Retries + DLQ.
/// - Manual commit (EnableAutoCommit=false)
/// - Duplicate detection via IProcessedMessageStore (fallback to in-memory)
/// - Immediate retries with exponential backoff
/// - DLQ publishing for poison/unrecoverable cases
/// </summary>
public sealed class KafkaEmailConsumer2 : BackgroundService
{
    private readonly ILogger<KafkaEmailConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _producer; // for DLQ (and future retry topics)
    private readonly string _topic;
    private readonly string _dlqTopic;
    private readonly int _maxProcessRetries;
    private readonly int[] _retryBackoffMs;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public KafkaEmailConsumer2(
        IConfiguration config,
        ILogger<KafkaEmailConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        _topic = config["Kafka:Topic"] ?? "email";
        _dlqTopic = config["Kafka:DlqTopic"] ?? "email.dlq";
        _maxProcessRetries = TryInt(config["Kafka:MaxProcessRetries"], 3);
        //_retryBackoffMs = ParseBackoffs(config["Kafka:RetryBackoffMs"]) ?? new[] { 500, 2000, 5000 };

        var commonBootstrap = config["Kafka:BootstrapServers"] ?? "kafka1:9092,kafka2:9092";

        // ----- CONSUMER -----
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = commonBootstrap,
            GroupId = config["Kafka:GroupId"] ?? "ConsumerGroupA",
            AutoOffsetReset = TryEnum(config["Kafka:AutoOffsetReset"], AutoOffsetReset.Earliest),
            EnableAutoCommit = false, // we commit manually only on success or after DLQ
            SessionTimeoutMs = TryInt(config["Kafka:SessionTimeoutMs"], 30000),
            MaxPollIntervalMs = TryInt(config["Kafka:MaxPollIntervalMs"], 300000),
            ReconnectBackoffMs = TryInt(config["Kafka:ReconnectBackoffMs"], 1000),
            ReconnectBackoffMaxMs = TryInt(config["Kafka:ReconnectBackoffMaxMs"], 10000),
        };

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal) _logger.LogError("Fatal Kafka error: {Reason}", e.Reason);
                else _logger.LogWarning("Kafka error: {Reason}", e.Reason);
            })
            .SetLogHandler((_, msg) =>
                _logger.LogDebug("Kafka[{Name}] {Level}: {Message}", msg.Name, msg.Level, msg.Message))
            .Build();

        // ----- PRODUCER (for DLQ / retry publishing) -----
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = commonBootstrap,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 10,
            LingerMs = 0,
            // Optional: TransactionalId if you later do Kafka->Kafka EOS flows
        };

        _producer = new ProducerBuilder<string, string>(producerConfig)
            .SetErrorHandler((_, e) => _logger.LogWarning("Producer error: {Reason}", e.Reason))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KafkaEmailConsumer starting. Topic: {Topic}, DLQ: {DLQ}", _topic, _dlqTopic);

        try
        {
            _consumer.Subscribe(_topic);
        }
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
                    if (result is null) continue;

                    // 1) Parse and extract messageId + DTO
                    if (!TryParseEnvelope(result.Message.Value, out var messageId, out var dto))
                    {
                        _logger.LogWarning("Bad/unknown payload at {TPO}. Sending to DLQ.", result.TopicPartitionOffset);
                        await PublishToDlqAsync(result, messageId ?? ComputeHash(result.Message.Value), "Invalid or unknown schema", 0, result.Message.Value, stoppingToken);
                        _consumer.Commit(result);
                        continue;
                    }

                    // 2) Scoped services (IEmailSender, optional IProcessedMessageStore)
                    using var scope = _scopeFactory.CreateScope();
                    var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                    // If a dedupe store is registered, use it; else in-memory fallback
                    var store = scope.ServiceProvider.GetService<IProcessedMessageStore>() ?? InMemoryProcessedMessageStore.Instance;

                    // 3) Idempotency check
                    if (await store.ExistsAsync(messageId, stoppingToken))
                    {
                        _logger.LogInformation("Duplicate message {MessageId} at {TPO} skipped.", messageId, result.TopicPartitionOffset);
                        _consumer.Commit(result);
                        continue;
                    }

                    // 4) Immediate retries with exponential backoff
                    var attempt = 0;
                    while (true)
                    {
                        try
                        {
                            await sender.SendAsync(dto!, stoppingToken);

                            // 5) Mark processed + commit
                            await store.MarkProcessedAsync(messageId, stoppingToken);
                            _consumer.Commit(result);

                            _logger.LogInformation("Processed {MessageId} at {TPO}.", messageId, result.TopicPartitionOffset);
                            break;
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw; // graceful shutdown
                        }
                        catch (Exception ex)
                        {
                            attempt++;
                            if (attempt > _maxProcessRetries)
                            {
                                _logger.LogError(ex, "Exceeded retries for {MessageId} at {TPO}. Quarantining to DLQ.",
                                    messageId, result.TopicPartitionOffset);
                                await PublishToDlqAsync(result, messageId, ex.Message, attempt - 1, result.Message.Value, stoppingToken);
                                _consumer.Commit(result); // commit original to move the partition forward
                                break;
                            }

                            var delay = _retryBackoffMs[Math.Min(attempt - 1, _retryBackoffMs.Length - 1)];
                            _logger.LogWarning(ex, "Transient failure for {MessageId} (attempt {Attempt}/{Max}). Backing off {Delay}ms.",
                                messageId, attempt, _maxProcessRetries, delay);
                            await Task.Delay(delay, stoppingToken);
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "ConsumeException at {TPO}: {Reason}", result?.TopicPartitionOffset, ex.Error.Reason);
                    await Task.Delay(1000, stoppingToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "JSON error at {TPO}; committing to skip poison message.", result?.TopicPartitionOffset);
                    if (result is not null) _consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // normal shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled processing error at {TPO}. Backing off briefly.", result?.TopicPartitionOffset);
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            try { _consumer.Close(); } catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
    }

    // ----------------- Helpers -----------------

    private static bool TryParseEnvelope(string payload, out string? messageId, out NotificationRequestDto? dto)
    {
        messageId = null;
        dto = null;

        // 1) Preferred: KafkaMessage<NotificationRequestDto>
        try
        {
            var envTyped = JsonSerializer.Deserialize<KafkaMessage<NotificationRequestDto>>(payload, JsonOpts);
            if (envTyped?.Content is not null)
            {
                messageId = !string.IsNullOrWhiteSpace(envTyped.Id) ? envTyped.Id : ComputeHash(payload);
                dto = envTyped.Content;
                return true;
            }
        }
        catch { /* fall through */ }

        // 2) Current: KafkaMessage<string> where Content is JSON of NotificationRequestDto
        try
        {
            var envRaw = JsonSerializer.Deserialize<KafkaMessage<string>>(payload, JsonOpts);
            if (!string.IsNullOrWhiteSpace(envRaw?.Content))
            {
                dto = JsonSerializer.Deserialize<NotificationRequestDto>(envRaw.Content, JsonOpts);
                if (dto is not null)
                {
                    messageId = !string.IsNullOrWhiteSpace(envRaw!.Id) ? envRaw.Id : ComputeHash(envRaw.Content);
                    return true;
                }
            }
        }
        catch { /* fall through */ }

        // 3) Bare DTO (no envelope)
        try
        {
            dto = JsonSerializer.Deserialize<NotificationRequestDto>(payload, JsonOpts);
            if (dto is not null)
            {
                messageId = ComputeHash(payload);
                return true;
            }
        }
        catch { }

        return false;
    }

    private async Task PublishToDlqAsync(
        ConsumeResult<string, string> result,
        string messageId,
        string error,
        int attempts,
        string originalPayload,
        CancellationToken ct)
    {
        var dead = new DeadLetter<string>
        {
            Id = Guid.NewGuid().ToString("N"),
            Original = originalPayload,
            Error = error,
            Attempts = attempts,
            SourceTopic = result.Topic,
            SourcePartition = result.Partition.Value,
            SourceOffset = result.Offset.Value,
            FailedAt = DateTime.UtcNow
        };

        var envelope = new KafkaMessage<DeadLetter<string>>
        {
            Id = messageId,
            Content = dead,
            Timestamp = DateTime.UtcNow
        };

        var msg = new Message<string, string>
        {
            Key = result.Message.Key ?? messageId,
            Value = JsonSerializer.Serialize(envelope, JsonOpts),
            Headers = CombineHeaders(result.Message.Headers, h => h.Key != "x-original-topic")
        };
        msg.Headers ??= new Headers();
        msg.Headers.Add("x-original-topic", Encoding.UTF8.GetBytes(result.Topic));

        await _producer.ProduceAsync(_dlqTopic, msg, ct);
        _logger.LogWarning("Published message {MessageId} to DLQ {DLQ} from {TPO}.",
            messageId, _dlqTopic, result.TopicPartitionOffset);
    }

    private static Headers? CombineHeaders(Headers? original, Func<Header, bool> filter)
    {
        if (original is null) return null;
        var h = new Headers();
        foreach (var header in original)
            if (filter((Header)header))
                h.Add(header.Key, header.GetValueBytes());
        return h;
    }

    private static string ComputeHash(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    private static int TryInt(string? v, int fallback) => int.TryParse(v, out var x) ? x : fallback;
    private static AutoOffsetReset TryEnum(string? v, AutoOffsetReset fallback) =>
        Enum.TryParse(v, out AutoOffsetReset e) ? e : fallback;

    public override void Dispose()
    {
        _consumer.Dispose();
        _producer.Dispose();
        base.Dispose();
    }

    // --------------- Local contracts / models ---------------

    /// <summary>Minimal dedupe store. Replace with a real EF/Redis implementation by registering IProcessedMessageStore in DI.</summary>
    public interface IProcessedMessageStore
    {
        Task<bool> ExistsAsync(string messageId, CancellationToken ct = default);
        Task MarkProcessedAsync(string messageId, CancellationToken ct = default);
    }

    private sealed class InMemoryProcessedMessageStore : IProcessedMessageStore
    {
        public static readonly InMemoryProcessedMessageStore Instance = new();
        private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
        private readonly object _lock = new();

        public Task<bool> ExistsAsync(string messageId, CancellationToken ct = default)
        {
            lock (_lock) { return Task.FromResult(_ids.Contains(messageId)); }
        }

        public Task MarkProcessedAsync(string messageId, CancellationToken ct = default)
        {
            lock (_lock) { _ids.Add(messageId); }
            return Task.CompletedTask;
        }
    }

    public sealed class KafkaMessage<T>
    {
        public string Id { get; set; } = string.Empty;
        public T? Content { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public sealed class DeadLetter<T>
    {
        public string Id { get; set; } = string.Empty;
        public T? Original { get; set; }
        public string Error { get; set; } = string.Empty;
        public int Attempts { get; set; }
        public string SourceTopic { get; set; } = string.Empty;
        public int SourcePartition { get; set; }
        public long SourceOffset { get; set; }
        public DateTime FailedAt { get; set; } = DateTime.UtcNow;
    }
}
