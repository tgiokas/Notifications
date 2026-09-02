namespace Notifications.Application.Interfaces;

/// Unit-of-work style persistence abstraction: just the commit contract.
/// Entity access (DbSet<T>) stays on the concrete ApplicationDbContext
/// (Infrastructure) — repositories that need it depend on that directly.
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
