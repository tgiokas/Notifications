using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Interfaces;

namespace Notifications.Infrastructure.Messaging;

/// Background worker that polls the OutboxMessages table and sends pending
/// messages via IEmailSender. This completes the Outbox Pattern for the
/// non-Kafka delivery mode:
///
/// 1. EmailsController -> EmailService -> OutboxEmailPublisher writes an
///    OutboxMessage row.
/// 2. OutboxProcessor picks up pending OutboxMessages.
/// 3. Sends each via IEmailSender (SendGrid/SMTP) — the same sender
///    KafkaEmailConsumer uses in Kafka mode.
/// 4. Marks as processed (or increments retry on failure).
///
/// If the mail provider is down, messages accumulate in the outbox and get
/// retried. If the service restarts, unprocessed messages are picked up again.
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxSettings _settings;
    private readonly ILogger<OutboxProcessor> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxSettings> options,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started. Polling every {Interval}ms", _settings.PollingIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxProcessor encountered an error");
            }

            await Task.Delay(_settings.PollingIntervalMs, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessor stopped.");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var pendingMessages = await outboxRepo.GetPendingAsync(
            batchSize: _settings.BatchSize,
            maxRetries: _settings.MaxAttempts);

        if (pendingMessages.Count == 0)
            return;

        _logger.LogDebug("Processing {Count} outbox messages", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                var emailDto = JsonSerializer.Deserialize<NotificationEmailDto>(message.Payload, JsonOpts)
                    ?? throw new InvalidOperationException("Payload deserialized to null.");

                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                await sender.SendAsync(emailDto, cancellationToken);

                await outboxRepo.MarkAsProcessedAsync(message.Id);

                _logger.LogDebug("Outbox message {EventId} sent for {Key}", message.EventId, message.Key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send outbox message {EventId} (retry {Retry})",
                    message.EventId, message.RetryCount);

                await outboxRepo.MarkAsFailedAsync(message.Id, ex.Message);

                if (message.RetryCount + 1 >= _settings.MaxAttempts)
                {
                    _logger.LogError(
                        "Outbox message {EventId} (type={EventType}, key={Key}) has exhausted all {Max} retries " +
                        "and will no longer be processed. Manual intervention required.",
                        message.EventId, message.EventType, message.Key, _settings.MaxAttempts);
                }
            }
        }
    }
}
