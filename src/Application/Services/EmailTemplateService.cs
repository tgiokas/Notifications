using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Domain.Interfaces;

namespace Notifications.Application.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IEmailTemplateRepository _repository;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(
        IEmailTemplateRepository repository,
        ILogger<EmailTemplateService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// Rendering (used by email senders)
    public async Task<string> RenderAsync(EmailTemplateType type, IDictionary<string, string> tokens)
    {
        var html = await ResolveHtmlAsync(type.ToString());
        return ReplaceTokens(html, tokens);
    }

    /// CRUD
    public async Task<IReadOnlyList<EmailTemplateResponseDto>> GetAllAsync(CancellationToken ct = default)
    {
        var templates = await _repository.GetAllAsync(ct);
        return templates.Select(MapToResponse).ToList();
    }

    public async Task<EmailTemplateResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct);
        return template is null ? null : MapToResponse(template);
    }

    public async Task<EmailTemplateResponseDto?> GetActiveByTypeAsync(string templateType, CancellationToken ct = default)
    {
        var template = await _repository.GetActiveByTypeAsync(templateType, ct);
        return template is null ? null : MapToResponse(template);
    }

    public async Task<EmailTemplateResponseDto> CreateAsync(EmailTemplateRequestDto request, CancellationToken ct = default)
    {
        var html = DecodeBase64(request.HtmlContentBase64);

        // Deactivate existing active template of the same type
        var existing = await _repository.GetActiveByTypeAsync(request.TemplateType, ct);
        if (existing is not null)
        {
            existing.IsActive = false;
            existing.ModifiedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(existing, ct);
        }

        var template = new EmailTemplate
        {
            TemplateType = request.TemplateType,
            Name = request.Name,
            Description = request.Description,
            HtmlContent = html,
            DefaultSubject = request.DefaultSubject,
            TokenDefinitions = request.TokenDefinitions,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(template, ct);

        _logger.LogInformation("Created template {Type} ({Id})", template.TemplateType, template.Id);

        return MapToResponse(template);
    }

    public async Task<EmailTemplateResponseDto> UpdateAsync(Guid id, EmailTemplateRequestDto request, CancellationToken ct = default)
    {
        var current = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template with id '{id}' not found.");

        var html = DecodeBase64(request.HtmlContentBase64);

        // Deactivate the current version
        current.IsActive = false;
        current.ModifiedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(current, ct);

        // Create a new version
        var newVersion = new EmailTemplate
        {
            TemplateType = request.TemplateType,
            Name = request.Name,
            Description = request.Description,
            HtmlContent = html,
            DefaultSubject = request.DefaultSubject,
            TokenDefinitions = request.TokenDefinitions,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(newVersion, ct);

        _logger.LogInformation("Updated template {Type} ({Id})", newVersion.TemplateType, newVersion.Id);

        return MapToResponse(newVersion);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template with id '{id}' not found.");

        await _repository.DeleteAsync(id, ct);
        
        _logger.LogInformation("Deleted template {Id}", id);
    }

    // ═══════════════════════════════════════════════════════════
    //  Preview & Activation
    // ═══════════════════════════════════════════════════════════

    public async Task<EmailTemplatePreviewResponseDto> PreviewAsync(Guid id, EmailTemplatePreviewRequestDto request, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template with id '{id}' not found.");

        var rendered = ReplaceTokens(template.HtmlContent, request.Tokens);

        return new EmailTemplatePreviewResponseDto
        {
            RenderedHtmlBase64 = EncodeBase64(rendered),
            TemplateType = template.TemplateType,
        };
    }

    public async Task<EmailTemplateResponseDto> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template with id '{id}' not found.");

        // Deactivate current active version of the same type
        var currentActive = await _repository.GetActiveByTypeAsync(template.TemplateType, ct);
        if (currentActive is not null && currentActive.Id != id)
        {
            currentActive.IsActive = false;
            currentActive.ModifiedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(currentActive, ct);
        }

        template.IsActive = true;
        template.ModifiedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(template, ct);
        //InvalidateCache(template.TemplateType);

        _logger.LogInformation("Activated template {Type} ({Id})", template.TemplateType, template.Id);

        return MapToResponse(template);
    }

    // ═══════════════════════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════════════════════

    private async Task<string?> ResolveHtmlAsync(string templateType)
    {
        //// 1) Check cache
        //if (_cache.TryGetValue(templateType, out var cached) &&
        //    DateTime.UtcNow - cached.CachedAt < CacheDuration)
        //{
        //    return cached.Html;
        //}

        // 2) Try database

        var dbTemplate = await _repository.GetActiveByTypeAsync(templateType);
        if (dbTemplate is not null)
        {
            //_cache[templateType] = (dbTemplate.HtmlContent, DateTime.UtcNow);
            _logger.LogDebug("Template '{Type}' loaded from DB.",
                templateType);
            return dbTemplate.HtmlContent;
        }

        return null;


        //// 3) Fallback to filesystem
        //var filePath = Path.Combine(_templateRoot, $"{templateType}.html");
        //if (!File.Exists(filePath))
        //    throw new FileNotFoundException(
        //        $"Template '{templateType}' not found in database or filesystem.");

        //var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        //_cache[templateType] = (html, DateTime.UtcNow);
        //_logger.LogDebug("Template '{Type}' loaded from filesystem (fallback).", templateType);

        //return html;
    }

    //private void InvalidateCache(string templateType)
    //{
    //    _cache.TryRemove(templateType, out _);
    //}

    private static string ReplaceTokens(string html, IDictionary<string, string>? tokens)
    {
        if (tokens is null || tokens.Count == 0) return html;
        foreach (var token in tokens)
            html = html.Replace($"{{{{{token.Key}}}}}", token.Value ?? string.Empty);
        return html;
    }

    private static EmailTemplateResponseDto MapToResponse(EmailTemplate t) => new()
    {
        Id = t.Id,
        TemplateType = t.TemplateType,
        Name = t.Name,
        Description = t.Description,
        HtmlContentBase64 = EncodeBase64(t.HtmlContent),
        DefaultSubject = t.DefaultSubject,
        TokenDefinitions = t.TokenDefinitions,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.ModifiedAt,
        CreatedBy = t.CreatedBy,
        UpdatedBy = t.ModifiedBy
    };

    private static string EncodeBase64(string text)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static string DecodeBase64(string base64)
        => Encoding.UTF8.GetString(Convert.FromBase64String(base64));
}
