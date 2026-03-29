using Microsoft.EntityFrameworkCore;

using Notifications.Domain.Entities;
using Notifications.Domain.Interfaces;
using Notifications.Infrastructure.Database;

namespace Notifications.Infrastructure.Repositories;

public class EmailTemplateRepository : IEmailTemplateRepository
{
    private readonly ApplicationDbContext _context;

    public EmailTemplateRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.EmailTemplates.FindAsync(new object[] { id }, ct);
    }

    public async Task<EmailTemplate?> GetActiveByTypeAsync(string templateType, CancellationToken ct = default)
    {
        return await _context.EmailTemplates
            .Where(t => t.TemplateType == templateType && t.IsActive)            
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<EmailTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.EmailTemplates
            .OrderBy(t => t.TemplateType)          
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmailTemplate>> GetVersionHistoryAsync(string templateType, CancellationToken ct = default)
    {
        return await _context.EmailTemplates
            .Where(t => t.TemplateType == templateType)            
            .ToListAsync(ct);
    }

    public async Task AddAsync(EmailTemplate template, CancellationToken ct = default)
    {
        await _context.EmailTemplates.AddAsync(template, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(EmailTemplate template, CancellationToken ct = default)
    {
        _context.EmailTemplates.Update(template);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _context.EmailTemplates.FindAsync(new object[] { id }, ct);
        if (template is not null)
        {
            _context.EmailTemplates.Remove(template);
            await _context.SaveChangesAsync(ct);
        }
    }
}
