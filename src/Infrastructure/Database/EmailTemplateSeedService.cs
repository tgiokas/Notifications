using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Notifications.Domain.Entities;
using Notifications.Domain.Enums;

namespace Notifications.Infrastructure.Database;

/// Seeds the EmailTemplates table from the filesystem templates on first run.
/// Runs as a hosted service so it executes once at startup.
public class EmailTemplateSeedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EmailTemplateSeedService> _logger;

    public EmailTemplateSeedService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment environment,
        ILogger<EmailTemplateSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Only seed if the table is empty
            if (await context.EmailTemplates.AnyAsync(cancellationToken))
            {
                _logger.LogDebug("EmailTemplates table already has data. Skipping seed.");
                return;
            }

            var templateDir = Path.Combine(_environment.ContentRootPath, "Templates", "Email");
            if (!Directory.Exists(templateDir))
            {
                _logger.LogWarning("Template directory not found at {Path}. Skipping seed.", templateDir);
                return;
            }

            // Map known enum types to metadata
            var knownTemplates = new Dictionary<EmailTemplateType, (string Name, string? Subject, string? Tokens)>
            {
                [EmailTemplateType.Generic] = ("Generic Email", null, null),
                [EmailTemplateType.VerificationLink] = ("Verification Link", "Verify your account", "Username,VerificationLink"),
                [EmailTemplateType.VerificationCode] = ("Verification Code", "Your verification code", "Username,VerificationCode"),
                [EmailTemplateType.MfaCode] = ("MFA Code", "Your MFA code", "Username,MfaCode"),
                [EmailTemplateType.PasswordReset] = ("Password Reset", "Reset your password", "Username,PasswordResetLink"),
            };

            foreach (var templateType in Enum.GetValues<EmailTemplateType>())
            {
                var filePath = Path.Combine(templateDir, $"{templateType}.html");
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("No file found for {Type}, skipping.", templateType);
                    continue;
                }

                var html = await File.ReadAllTextAsync(filePath, cancellationToken);
                var meta = knownTemplates.GetValueOrDefault(templateType, (templateType.ToString(), null, null));

                var entity = new EmailTemplate
                {
                    TemplateType = templateType.ToString(),
                    Name = meta.Name,
                    Description = $"Seeded from filesystem on {DateTime.UtcNow:u}",
                    HtmlContent = html,
                    DefaultSubject = meta.Subject,
                    TokenDefinitions = meta.Tokens,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system-seed"
                };

                await context.EmailTemplates.AddAsync(entity, cancellationToken);
                _logger.LogInformation("Seeded template '{Type}' from filesystem.", templateType);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Email template seeding failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
