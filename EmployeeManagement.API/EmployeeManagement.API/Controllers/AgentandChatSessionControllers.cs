using EmployeeManagement.Application.DTOs.Agent;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly IAgentOrchestratorService _orchestrator;
    public AgentController(IAgentOrchestratorService orchestrator) => _orchestrator = orchestrator;

    /// <summary>
    /// Main chatbot entry point. Pass sessionId to continue a conversation,
    /// or omit it to start a new one. The response tells you which intent
    /// was detected and which internal tool (if any) handled it.
    /// </summary>
    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, CancellationToken ct)
    {
        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (!int.TryParse(employeeIdClaim, out var employeeId))
            return Unauthorized(new { error = "EmployeeId claim missing from token." });

        try
        {
            return Ok(await _orchestrator.HandleMessageAsync(employeeId, dto, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatSessionController : ControllerBase
{
    private readonly IChatSessionService _sessionService;
    public ChatSessionController(IChatSessionService sessionService) => _sessionService = sessionService;

    [HttpGet("my")]
    public async Task<IActionResult> GetMySessions(CancellationToken ct)
    {
        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (!int.TryParse(employeeIdClaim, out var employeeId))
            return Unauthorized(new { error = "EmployeeId claim missing from token." });

        return Ok(await _sessionService.GetSessionsAsync(employeeId, ct));
    }

    [HttpGet("{sessionId:int}/messages")]
    public async Task<IActionResult> GetMessages(int sessionId, CancellationToken ct)
    {
        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (!int.TryParse(employeeIdClaim, out var employeeId))
            return Unauthorized(new { error = "EmployeeId claim missing from token." });

        try
        {
            return Ok(await _sessionService.GetMessagesAsync(sessionId, employeeId, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
