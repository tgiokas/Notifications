using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Confluent.Kafka;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Microsoft.Extensions.Options;
using Notifications.Application.Configuration;

namespace Notifications.Infrastructure.Messaging;

public sealed class KafkaEmailConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;    
    private readonly ILogger<KafkaEmailConsumer> _logger;
    private readonly string[] _topics;

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

        var consumerConfig = new ConsumerConfig
        {       
            BootstrapServers = settings.BootstrapServers,           
            ReconnectBackoffMs = settings.ReconnectBackoffMs,           
            ReconnectBackoffMaxMs = settings.ReconnectBackoffMaxMs,            
            GroupId = settings.GroupId,
            AutoOffsetReset = settings.AutoOffsetReset,
            EnableAutoCommit = settings.EnableAutoCommit,
            AutoCommitIntervalMs = settings.AutoCommitIntervalMs,            
            SessionTimeoutMs = settings.SessionTimeoutMs,
            MaxPollIntervalMs = settings.MaxPollIntervalMs,           
            ApiVersionRequestTimeoutMs = settings.ApiVersionRequestTimeoutMs
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
        // Yield to let the rest of the app start
        await Task.Yield();

        _logger.LogInformation("KafkaEmailConsumer starting. Topics: {Topics}", string.Join(",", _topics));

        // Retry subscribe until Kafka is ready
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _consumer.Subscribe(_topics);
                _logger.LogInformation("Successfully subscribed to topics.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka not ready, retrying in 5s...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? result = null;

                try
                {
                    result = _consumer.Consume(TimeSpan.FromSeconds(5));
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
