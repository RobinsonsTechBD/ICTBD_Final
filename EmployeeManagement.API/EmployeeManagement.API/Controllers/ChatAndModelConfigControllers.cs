using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IModelFallbackChatService _chatService;
    public ChatController(IModelFallbackChatService chatService) => _chatService = chatService;

    /// <summary>
    /// Test endpoint for Phase B. Sends messages through the fallback chain
    /// and returns which model actually answered plus the full attempt log —
    /// useful for proving the fallback behavior works (stop a model, watch it skip to the next).
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] ChatCompletionRequestDto request, CancellationToken ct)
    {
        try
        {
            return Ok(await _chatService.CompleteAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            var attempts = ex.Data.Contains("Attempts") ? ex.Data["Attempts"] : null;
            return StatusCode(503, new { error = ex.Message, attempts });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AIModelConfigController : ControllerBase
{
    private readonly IAIModelConfigService _configService;
    public AIModelConfigController(IAIModelConfigService configService) => _configService = configService;

    [HttpGet]
    public async Task<IActionResult> GetChain(CancellationToken ct) => Ok(await _configService.GetChainAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AIModelConfigDto dto, CancellationToken ct)
        => Ok(await _configService.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AIModelConfigDto dto, CancellationToken ct)
    {
        await _configService.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ReorderModelDto> newOrder, CancellationToken ct)
    {
        await _configService.ReorderAsync(newOrder, ct);
        return NoContent();
    }

    /// <summary>Pings every enabled model and records reachability — run this before a demo.</summary>
    [HttpPost("health-check")]
    public async Task<IActionResult> HealthCheck(CancellationToken ct) => Ok(await _configService.RunHealthCheckAsync(ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _configService.DeleteAsync(id, ct);
        return NoContent();
    }
}
