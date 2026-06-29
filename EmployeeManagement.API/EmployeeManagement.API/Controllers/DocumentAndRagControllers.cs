using EmployeeManagement.Application.DTOs.Rag;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    public DocumentController(IDocumentService documentService) => _documentService = documentService;

    /// <summary>
    /// Phase C accepts raw extracted text (paste .txt/.md content, or text already
    /// extracted client-side from a PDF/DOCX). Binary PDF/DOCX upload + server-side
    /// extraction is a natural Phase C+ enhancement once this pipeline is proven.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Upload([FromBody] UploadDocumentDto dto, CancellationToken ct)
    {
        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (!int.TryParse(employeeIdClaim, out var employeeId))
            return Unauthorized(new { error = "EmployeeId claim missing from token." });

        return Ok(await _documentService.UploadAndIndexAsync(dto, employeeId, ct));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _documentService.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _documentService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _documentService.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RagController : ControllerBase
{
    private readonly IRagQueryService _ragQueryService;
    public RagController(IRagQueryService ragQueryService) => _ragQueryService = ragQueryService;

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] AskQuestionDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _ragQueryService.AskAsync(dto, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
