using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Confluent.Kafka;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

using Notifications.Application.Configuration;

namespace Notifications.Infrastructure.Messaging;

public sealed class KafkaEmailConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaEmailConsumer> _logger;
    private readonly ConsumerConfig _consumerConfig;
    private readonly string[] _topics;    
    private volatile bool _fatalError;

    private const int RebuildDelayMs = 5000;
    private const int TransientBackoffMs = 1000;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KafkaEmailConsumer(IOptions<KafkaSettings> kafkaOptions,
                              ILogger<KafkaEmailConsumer> logger,
                              IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        var settings = kafkaOptions.Value;
        _topics = settings.Topics;

        // Config is built once and reused for every (re)built consumer instance.
        _consumerConfig = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            ReconnectBackoffMs = settings.ReconnectBackoffMs,
            ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,
            SocketConnectionSetupTimeoutMs = settings.SocketConnectionSetupTimeoutMs,
            SocketTimeoutMs = settings.SocketTimeoutMs,

            GroupId = settings.GroupId,
            AutoOffsetReset = settings.AutoOffsetReset,
            EnableAutoCommit = false,
            SessionTimeoutMs = settings.SessionTimeoutMs,
            MaxPollIntervalMs = settings.MaxPollIntervalMs,
        };

        _consumerConfig.ApplySasl(settings);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to let the rest of the app start
        await Task.Yield();

        _logger.LogInformation("KafkaEmailConsumer starting. Topics: {Topics}", string.Join(",", _topics));

        // Outer lifecycle loop: each iteration owns one consumer instance.
        // If that instance dies (fatal error / unexpected crash) we dispose it and build a fresh one.
        while (!stoppingToken.IsCancellationRequested)
        {
            IConsumer<string, string>? consumer = null;

            try
            {
                consumer = BuildConsumer();

                // Subscribe to topics
                consumer.Subscribe(_topics);
                _logger.LogInformation("Subscribed to topics.");

                await ConsumeLoop(consumer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consumer instance crashed. Rebuilding in {Delay}ms...", RebuildDelayMs);
            }
            finally
            {
                if (consumer is not null)
                {
                    try { consumer.Close(); }      // leave group & commit last offsets
                    catch (Exception ex) { _logger.LogWarning(ex, "Error closing Kafka consumer."); }
                    consumer.Dispose();
                }
            }

            // Pause before rebuilding so we don't hot-loop while the broker is still down.
            if (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(RebuildDelayMs, stoppingToken); }
                catch (OperationCanceledException) { /* shutting down */ }
            }
        }
    }

    private IConsumer<string, string> BuildConsumer()
    {
        _fatalError = false;

        return new ConsumerBuilder<string, string>(_consumerConfig)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                {
                    // A fatal error means THIS consumer instance is dead and can
                    // only be recovered by disposing it and creating a new one.
                    _logger.LogError("Fatal Kafka error: {Reason}. Consumer will be rebuilt.", e.Reason);
                    _fatalError = true;
                }
                else
                {
                    // Transient (e.g. "all brokers down" during an outage) – librdkafka
                    // will keep reconnecting on its own using the backoff settings.
                    _logger.LogWarning("Kafka error: {Reason}", e.Reason);
                }
            })
            .SetLogHandler((_, msg) =>
            {
                _logger.LogDebug("Kafka[{Name}] {Level}: {Message}", msg.Name, msg.Level, msg.Message);
            })
            .Build();
    }

    private async Task ConsumeLoop(IConsumer<string, string> consumer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // The error handler may flag a fatal state on a background thread.
            // Break out so the outer loop can rebuild the consumer.
            if (_fatalError)
            {
                _logger.LogWarning("Fatal error flagged; tearing down consumer to rebuild.");
                return;
            }

            ConsumeResult<string, string>? result = null;

            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(5));
                if (result == null) continue; // timeout, no message (e.g. broker down) -> keep polling

                _logger.LogInformation("Message from topic {Topic} consumed", result.Topic);

                var emailDto = ParsePayload(result.Message.Value);
                if (emailDto == null)
                {
                    _logger.LogWarning("Skipping message at {TPO}: unable to parse payload.", result.TopicPartitionOffset);
                    consumer.Commit(result); // commit to avoid reprocessing a poison message
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                await sender.SendAsync(emailDto, stoppingToken);

                consumer.Commit(result); // success
                _logger.LogInformation("Offset committed at {TPO}", result.TopicPartitionOffset);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // normal shutdown
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "ConsumeException at {TPO}: {Reason}",
                    result?.TopicPartitionOffset, ex.Error.Reason);

                if (ex.Error.IsFatal)
                {
                    _logger.LogError("ConsumeException is fatal; rebuilding consumer.");
                    return; // outer loop rebuilds
                }

                await Task.Delay(TransientBackoffMs, stoppingToken);
            }
            catch (KafkaException ex)
            {
                // e.g. Commit() failing while the broker is unreachable.
                _logger.LogError(ex, "KafkaException at {TPO}: {Reason}",
                    result?.TopicPartitionOffset, ex.Error.Reason);

                if (ex.Error.IsFatal)
                    return;

                await Task.Delay(TransientBackoffMs, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled processing error at {TPO}. Backing off briefly.",
                    result?.TopicPartitionOffset);
                await Task.Delay(TransientBackoffMs, stoppingToken);
            }
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
}