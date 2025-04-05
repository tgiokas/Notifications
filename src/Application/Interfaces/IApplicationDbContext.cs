using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;

namespace Notifications.Application.Interfaces;

public interface IApplicationDbContext
{
    //DbSet<User> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
