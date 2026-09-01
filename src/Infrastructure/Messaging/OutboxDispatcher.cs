using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Notifications.Application.Configuration;
using Notifications.Application.Dtos;
using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Messaging;

/// Polls the outbox store and drives due messages through IEmailSender.
/// The non-Kafka counterpart to KafkaEmailConsumer: same retry/backoff idea,
/// but leasing rows from a local store instead of consuming a topic.
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxSettings _settings;
    private readonly ILogger<OutboxDispatcher> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxSettings> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _logger.LogInformation("OutboxDispatcher starting. Polling every {Interval}ms", _settings.PollingIntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error while processing outbox batch.");
            }

            try { await Task.Delay(_settings.PollingIntervalMs, stoppingToken); }
            catch (OperationCanceledException) { /* shutting down */ }
        }
    }

    private async Task ProcessDueBatchAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<OutboxMessage> batch;

        using (var scope = _scopeFactory.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            batch = await store.LeaseBatchAsync(_settings.BatchSize, stoppingToken);
        }

        if (batch.Count == 0) return;

        _logger.LogInformation("Leased {Count} outbox message(s).", batch.Count);

        foreach (var message in batch)
        {
            if (stoppingToken.IsCancellationRequested) return;
            await DeliverAsync(message, stoppingToken);
        }
    }

    private async Task DeliverAsync(OutboxMessage message, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        NotificationEmailDto? emailDto;
        try
        {
            emailDto = JsonSerializer.Deserialize<NotificationEmailDto>(message.Payload, JsonOpts);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Outbox message {Id} has an unparseable payload; marking dead.", message.Id);
            await store.MarkDeadAsync(message.Id, "Unable to deserialize payload.", stoppingToken);
            return;
        }

        if (emailDto is null)
        {
            await store.MarkDeadAsync(message.Id, "Payload deserialized to null.", stoppingToken);
            return;
        }

        try
        {
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            await sender.SendAsync(emailDto, stoppingToken);
            await store.MarkSentAsync(message.Id, stoppingToken);
        }
        catch (TransientDeliveryException ex)
        {
            await HandleFailureAsync(store, message, ex, stoppingToken);
        }
        catch (Exception ex)
        {
            // Unexpected error resolving/calling the sender; treat like a transient
            // failure rather than dropping silently.
            await HandleFailureAsync(store, message, ex, stoppingToken);
        }
    }

    private async Task HandleFailureAsync(IOutboxStore store, OutboxMessage message, Exception ex, CancellationToken ct)
    {
        var attempt = message.Attempts + 1;

        if (attempt >= _settings.MaxAttempts)
        {
            _logger.LogCritical(ex, "Outbox message {Id} failed after {Attempts} attempts; marking dead.", message.Id, attempt);
            await store.MarkDeadAsync(message.Id, ex.Message, ct);
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        var nextAttemptAt = DateTime.UtcNow.Add(delay);

        _logger.LogWarning(ex, "Outbox message {Id} failed (attempt {Attempt}/{Max}); retrying at {NextAttempt}.",
            message.Id, attempt, _settings.MaxAttempts, nextAttemptAt);

        await store.ScheduleRetryAsync(message.Id, ex.Message, nextAttemptAt, ct);
    }
}
