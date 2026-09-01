using System.Text.Json;
using Microsoft.EntityFrameworkCore;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public class EfOutboxStore : IOutboxStore
{
    private readonly NotificationsDbContext _db;

    public EfOutboxStore(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task EnqueueAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default)
    {
        var message = new OutboxMessage
        {
            Channel = "email",
            Payload = JsonSerializer.Serialize(emailDto)
        };

        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> LeaseBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Single-writer assumption: one OutboxDispatcher instance. A multi-instance
        // deployment would need row-level locking (e.g. SKIP LOCKED) to lease safely.
        var batch = await _db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending
                     && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
            message.Status = OutboxMessageStatus.Processing;

        await _db.SaveChangesAsync(cancellationToken);
        return batch;
    }

    public async Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync(new object[] { id }, cancellationToken);
        if (message is null) return;

        message.Status = OutboxMessageStatus.Sent;
        message.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ScheduleRetryAsync(Guid id, string error, DateTime nextAttemptAt, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync(new object[] { id }, cancellationToken);
        if (message is null) return;

        message.Status = OutboxMessageStatus.Pending;
        message.Attempts += 1;
        message.LastError = error;
        message.NextAttemptAt = nextAttemptAt;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkDeadAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await _db.OutboxMessages.FindAsync(new object[] { id }, cancellationToken);
        if (message is null) return;

        message.Status = OutboxMessageStatus.Dead;
        message.Attempts += 1;
        message.LastError = error;
        message.ProcessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
