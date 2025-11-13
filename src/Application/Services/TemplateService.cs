using System.Text;
using Microsoft.Extensions.Hosting;

using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;

namespace Notifications.Application.Services;

public class TemplateService : ITemplateService
{
    private readonly string _templateRoot;

    public TemplateService(IHostEnvironment environment)
    {      
        _templateRoot = Path.Combine(environment.ContentRootPath, "Templates", "Email");
    }

    public async Task<string> RenderAsync(EmailTemplateType type, IDictionary<string, string>? parameteres)
    {
        var filePath = Path.Combine(_templateRoot, $"{type}.html");

        if (!File.Exists(filePath))
        {            
            throw new FileNotFoundException($"Template {type}.html not found");
        }

        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);

        if (parameteres is null || parameteres.Count == 0)
            return html;

        foreach (var token in parameteres)
        {
            html = html.Replace($"{{{{{token.Key}}}}}", token.Value ?? string.Empty);
        }        

        return html;
    }
}
