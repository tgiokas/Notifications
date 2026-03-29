using Notifications.Application.Dtos;
using Notifications.Domain.Enums;

namespace Notifications.Application.Interfaces;

public interface IEmailTemplateService
{
    // Rendering (used by email senders)
    Task<string> RenderAsync(EmailTemplateType type, IDictionary<string, string> tokens);

    // CRUD
    Task<IReadOnlyList<EmailTemplateResponseDto>> GetAllAsync(CancellationToken ct = default);
    Task<EmailTemplateResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EmailTemplateResponseDto?> GetActiveByTypeAsync(string templateType, CancellationToken ct = default);
    //Task<IReadOnlyList<EmailTemplateResponseDto>> GetVersionHistoryAsync(string templateType, CancellationToken ct = default);
    Task<EmailTemplateResponseDto> CreateAsync(EmailTemplateRequestDto request, CancellationToken ct = default);
    Task<EmailTemplateResponseDto> UpdateAsync(Guid id, EmailTemplateRequestDto request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Preview and activation
    Task<EmailTemplatePreviewResponseDto> PreviewAsync(Guid id, EmailTemplatePreviewRequestDto request, CancellationToken ct = default);
    Task<EmailTemplateResponseDto> ActivateAsync(Guid id, CancellationToken ct = default);
}
