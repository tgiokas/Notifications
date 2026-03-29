using Notifications.Domain.Entities;

namespace Notifications.Domain.Interfaces;

public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EmailTemplate?> GetActiveByTypeAsync(string templateType, CancellationToken ct = default);
    Task<IReadOnlyList<EmailTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmailTemplate>> GetVersionHistoryAsync(string templateType, CancellationToken ct = default);
    Task AddAsync(EmailTemplate template, CancellationToken ct = default);
    Task UpdateAsync(EmailTemplate template, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
