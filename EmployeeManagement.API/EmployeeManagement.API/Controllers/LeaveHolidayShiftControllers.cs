using EmployeeManagement.Application.DTOs.Attendance;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveController : ControllerBase
{
    private readonly ILeaveService _leaveService;
    public LeaveController(ILeaveService leaveService) => _leaveService = leaveService;

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll([FromQuery] LeaveQueryParams query, CancellationToken ct)
        => Ok(await _leaveService.GetLeaveRequestsAsync(query, ct));

    /// <summary>Any role (Marketing, Sales, Purchase, Delivery, etc.) can apply for their own leave.</summary>
    [HttpPost]
    public async Task<IActionResult> Apply([FromBody] CreateLeaveRequestDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _leaveService.CreateAsync(dto, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Approve/Reject — Admin or Manager only. ActionedBy is read from the JWT (NameIdentifier mapped to EmployeeId claim).</summary>
    [HttpPut("{id:int}/action")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Action(int id, [FromBody] LeaveActionDto dto, CancellationToken ct)
    {
        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (!int.TryParse(employeeIdClaim, out var actionedBy))
            return Unauthorized(new { error = "EmployeeId claim missing from token." });

        try
        {
            return Ok(await _leaveService.ActionAsync(id, actionedBy, dto, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HolidayController : ControllerBase
{
    private readonly IHolidayService _holidayService;
    public HolidayController(IHolidayService holidayService) => _holidayService = holidayService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? year, CancellationToken ct)
        => Ok(await _holidayService.GetAllAsync(year, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] HolidayDto dto, CancellationToken ct)
        => Ok(await _holidayService.CreateAsync(dto, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _holidayService.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShiftController : ControllerBase
{
    private readonly IShiftService _shiftService;
    public ShiftController(IShiftService shiftService) => _shiftService = shiftService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _shiftService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] WorkShiftDto dto, CancellationToken ct)
        => Ok(await _shiftService.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] WorkShiftDto dto, CancellationToken ct)
        => Ok(await _shiftService.UpdateAsync(id, dto, ct));
}
