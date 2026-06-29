using EmployeeManagement.Application.DTOs.SqlAi;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SqlQueryController : ControllerBase
{
    private readonly INlQueryService _nlQueryService;

    public SqlQueryController(INlQueryService nlQueryService)
        => _nlQueryService = nlQueryService;

    /// <summary>
    /// PREVIEW: Generates SQL from your question but does NOT execute it.
    /// Always safe to call — use this to review the SQL before committing to execution.
    /// Returns generatedSql + isSafe + (if unsafe) safetyRejectionReason.
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] NlQueryRequestDto dto, CancellationToken ct)
    {
        dto.ExecuteQuery = false;   // enforce preview mode regardless of what the caller sent
        var role = GetUserRole();
        return Ok(await _nlQueryService.QueryAsync(dto, role, ct));
    }

    /// <summary>
    /// EXECUTE: Generates SQL, runs the safety guard, and if safe, executes
    /// against the live database. Returns columns, rows, row count, truncation
    /// flag, and a plain-English summary of the results.
    /// Admin and Manager only — other roles get the preview endpoint.
    /// </summary>
    [HttpPost("execute")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Execute([FromBody] NlQueryRequestDto dto, CancellationToken ct)
    {
        dto.ExecuteQuery = true;
        var role = GetUserRole();

        try
        {
            return Ok(await _nlQueryService.QueryAsync(dto, role, ct));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// SCHEMA: Returns the schema context the AI would receive for your role,
    /// so you can understand exactly what tables/columns it knows about.
    /// Useful for framing well-targeted questions.
    /// </summary>
    [HttpGet("schema")]
    public IActionResult GetSchema([FromServices] ISchemaContextService schemaContext)
    {
        var role = GetUserRole();
        return Ok(new { role, schema = schemaContext.GetSchemaForRole(role) });
    }

    private string GetUserRole() =>
        User.FindFirstValue(ClaimTypes.Role) ?? "Employee";
}