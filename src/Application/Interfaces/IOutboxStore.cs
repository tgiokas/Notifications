using Notifications.Application.Dtos;
using Notifications.Domain.Entities;

namespace Notifications.Application.Interfaces;

/// Durable store backing the outbox delivery mode. Kept persistence-agnostic
/// here; the concrete implementation (EF Core / SQLite) lives in Infrastructure.
public interface IOutboxStore
{
    Task EnqueueAsync(NotificationEmailDto emailDto, CancellationToken cancellationToken = default);

    /// Leases up to batchSize due messages (Pending, or Pending with an elapsed
    /// NextAttemptAt) by flipping them to Processing so a single dispatcher
    /// instance doesn't reprocess a message still in flight.
    Task<IReadOnlyList<OutboxMessage>> LeaseBatchAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default);

    /// Reverts the message to Pending with a future NextAttemptAt after a transient failure.
    Task ScheduleRetryAsync(Guid id, string error, DateTime nextAttemptAt, CancellationToken cancellationToken = default);

    /// Marks the message as permanently failed (retry budget exhausted); it is not leased again.
    Task MarkDeadAsync(Guid id, string error, CancellationToken cancellationToken = default);
}
