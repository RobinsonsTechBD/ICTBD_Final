using EmployeeManagement.Application.DTOs.Vision;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisionController : ControllerBase
{
    private readonly IVisionAnalysisService _visionAnalysis;
    private readonly IVisionIndexService _visionIndex;

    private static readonly HashSet<string> AllowedMimeTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public VisionController(IVisionAnalysisService visionAnalysis, IVisionIndexService visionIndex)
    {
        _visionAnalysis = visionAnalysis;
        _visionIndex = visionIndex;
    }

    /// <summary>
    /// Analyse an image from a JSON body (base64). Good for Swagger testing.
    /// POST /api/vision/analyse
    /// </summary>
    [HttpPost("analyse")]
    public async Task<IActionResult> Analyse([FromBody] VisionAnalysisRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ImageBase64))
            return BadRequest(new { error = "ImageBase64 is required." });

        try
        {
            return Ok(await _visionAnalysis.AnalyseAsync(dto, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Analyse an image uploaded as multipart/form-data. More natural for real client usage.
    /// POST /api/vision/analyse-upload
    /// </summary>
    [HttpPost("analyse-upload")]
    [RequestSizeLimit(20 * 1024 * 1024)]   // 20 MB max
    public async Task<IActionResult> AnalyseUpload(
        IFormFile file,
        [FromForm] VisionTaskType taskType = VisionTaskType.GeneralDescription,
        [FromForm] string? customPrompt = null,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (!AllowedMimeTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"Unsupported file type '{file.ContentType}'. Use JPEG, PNG, or WebP." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var base64 = Convert.ToBase64String(ms.ToArray());

        var dto = new VisionAnalysisRequestDto
        {
            ImageBase64 = base64,
            MimeType = file.ContentType,
            TaskType = taskType,
            CustomPrompt = customPrompt
        };

        try
        {
            return Ok(await _visionAnalysis.AnalyseAsync(dto, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            return StatusCode(504, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Index an image into the RAG vector store: LLaVA describes the image,
    /// the description is chunked + embedded, and stored as a searchable
    /// Document — queryable via POST /api/rag/ask like any text document.
    /// POST /api/vision/index
    /// </summary>
    [HttpPost("index")]
    [Authorize(Roles = "Admin,Manager")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> IndexImage(
        IFormFile file,
        [FromForm] string title,
        CancellationToken ct)
    {
        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (!int.TryParse(employeeIdClaim, out var employeeId))
            return Unauthorized(new { error = "EmployeeId claim missing." });

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        if (!AllowedMimeTypes.Contains(file.ContentType))
            return BadRequest(new { error = $"Unsupported file type '{file.ContentType}'." });

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "title form field is required." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var base64 = Convert.ToBase64String(ms.ToArray());

        try
        {
            var result = await _visionIndex.IndexImageAsync(new VisionIndexRequestDto
            {
                ImageBase64 = base64,
                MimeType = file.ContentType,
                DocumentTitle = title,
                FileName = file.FileName
            }, employeeId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
