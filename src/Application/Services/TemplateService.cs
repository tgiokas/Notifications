using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;
using Notifications.Domain.Interfaces;

namespace Notifications.Application.Services;

/// Renders email templates by resolving HTML from the database (active version)
/// with an in-memory cache. Falls back to filesystem templates for backward
/// compatibility during migration.

public class TemplateService : ITemplateService
{
    private readonly IEmailTemplateRepository _templateRepository;
    private readonly ILogger<TemplateService> _logger;
    private readonly string _templateRoot;

    // Simple in-memory cache: templateType -> (html, cachedAtUtc)
    private readonly ConcurrentDictionary<string, (string Html, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public TemplateService(
        IEmailTemplateRepository templateRepository,
        IHostEnvironment environment,
        ILogger<TemplateService> logger)
    {
        _templateRepository = templateRepository;
        _logger = logger;
        _templateRoot = Path.Combine(environment.ContentRootPath, "Templates", "Email");
    }

    public async Task<string> RenderAsync(EmailTemplateType type, IDictionary<string, string> tokens)
    {
        var templateType = type.ToString();
        var html = await ResolveHtmlAsync(templateType);

        if (tokens is null || tokens.Count == 0)
            return html;

        foreach (var token in tokens)
        {
            html = html.Replace($"{{{{{token.Key}}}}}", token.Value ?? string.Empty);
        }

        return html;
    }

    private async Task<string> ResolveHtmlAsync(string templateType)
    {
        // 1) Check cache
        if (_cache.TryGetValue(templateType, out var cached) &&
            DateTime.UtcNow - cached.CachedAt < CacheDuration)
        {
            return cached.Html;
        }

        // 2) Try database
        try
        {
            var dbTemplate = await _templateRepository.GetActiveByTypeAsync(templateType);
            if (dbTemplate is not null)
            {
                _cache[templateType] = (dbTemplate.HtmlContent, DateTime.UtcNow);
                _logger.LogDebug("Template '{Type}' loaded from database (v{Version}).",
                    templateType, dbTemplate.Version);
                return dbTemplate.HtmlContent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load template '{Type}' from database. Falling back to filesystem.", templateType);
        }

        // 3) Fallback to filesystem (backward compatibility)
        var filePath = Path.Combine(_templateRoot, $"{templateType}.html");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template '{templateType}' not found in database or filesystem.");
        }

        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        _cache[templateType] = (html, DateTime.UtcNow);
        _logger.LogDebug("Template '{Type}' loaded from filesystem (fallback).", templateType);

        return html;
    }
}
