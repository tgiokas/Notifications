using Microsoft.EntityFrameworkCore;

using Notifications.Domain.Entities;
using Notifications.Domain.Interfaces;
using Notifications.Infrastructure.Database;

namespace Notifications.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly ApplicationDbContext _dbContext;

    public OutboxRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<OutboxMessage>> GetPendingAsync(int batchSize, int maxRetries)
    {
        return await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(o => o.ProcessedAt == null && o.RetryCount < maxRetries)
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }

    public async Task AddAsync(OutboxMessage message)
    {
        await _dbContext.OutboxMessages.AddAsync(message);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task MarkAsProcessedAsync(int id)
    {
        await _dbContext.OutboxMessages
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ProcessedAt, DateTime.UtcNow));
    }

    public async Task MarkAsFailedAsync(int id, string error)
    {
        await _dbContext.OutboxMessages
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.RetryCount, o => o.RetryCount + 1)
                .SetProperty(o => o.LastAttemptAt, DateTime.UtcNow)
                .SetProperty(o => o.Error, error));
    }
}
