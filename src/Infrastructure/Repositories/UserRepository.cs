using Microsoft.EntityFrameworkCore;

using DMS.Auth.Domain.Entities;
using DMS.Auth.Domain.Interfaces;
using DMS.Auth.Infrastructure.Persistence;

namespace DMS.Auth.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Users.FindAsync(id);
    }

    public async Task<User?> GetByUsernameAsync(string username, string agencyId)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.AgencyId == agencyId);
    }

    public async Task AddAsync(User user)
    {
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(User user)
    {
        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();
    }
}

