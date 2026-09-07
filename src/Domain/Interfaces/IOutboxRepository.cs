using Notifications.Domain.Entities;

namespace Notifications.Domain.Interfaces;

public interface IOutboxRepository
{
    Task<List<OutboxMessage>> GetPendingAsync(int batchSize, int maxRetries);

    // No SaveChanges — the caller (OutboxEmailPublisher) commits explicitly via
    // SaveChangesAsync. There's no separate aggregate write to join here (unlike
    // the reference design this follows, which commits an outbox row alongside
    // a domain entity insert in the same transaction), but keeping the shapes
    // apart still lets a future caller batch this with other writes.
    Task AddAsync(OutboxMessage message);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task MarkAsProcessedAsync(int id);

    /// Transient failure, still within the retry budget: increments RetryCount,
    /// leaves ProcessedAt null so it's picked up again by GetPendingAsync.
    Task MarkAsFailedAsync(int id, string error);

    /// Retry budget exhausted: sets ProcessedAt and Discarded=true so it's
    /// excluded from GetPendingAsync and distinguishable from a sent message.
    Task MarkAsDiscardedAsync(int id, string error);
}
