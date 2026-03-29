using Microsoft.AspNetCore.Mvc;

using Notifications.Application.Dtos;
using Notifications.Application.Interfaces;

namespace Notifications.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailTemplateController : ControllerBase
{
    private readonly IEmailTemplateService _templateService;

    public EmailTemplateController(IEmailTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _templateService.GetAllAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _templateService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("active/{templateType}")]
    public async Task<IActionResult> GetActiveByType(string templateType, CancellationToken ct)
    {
        var result = await _templateService.GetActiveByTypeAsync(templateType, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmailTemplateRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.HtmlContentBase64))
            return BadRequest("HtmlContentBase64 is required.");

        var result = await _templateService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EmailTemplateRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.HtmlContentBase64))
            return BadRequest("HtmlContentBase64 is required.");

        var result = await _templateService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _templateService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, [FromBody] EmailTemplatePreviewRequestDto request, CancellationToken ct)
    {
        var result = await _templateService.PreviewAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var result = await _templateService.ActivateAsync(id, ct);
        return Ok(result);
    }
}
