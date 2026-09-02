using Microsoft.EntityFrameworkCore;

using Notifications.Domain.Entities;

namespace Notifications.Application.Interfaces;

/// Persistence abstraction for the outbox store, so callers depend on this
/// interface instead of the concrete EF Core ApplicationDbContext (Infrastructure).
public interface IApplicationDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
