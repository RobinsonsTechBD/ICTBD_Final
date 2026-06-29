using EmployeeManagement.Application.DTOs.Attendance;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    private readonly IAttendanceDeviceSimulatorService _simulator;

    public AttendanceController(IAttendanceService attendanceService, IAttendanceDeviceSimulatorService simulator)
    {
        _attendanceService = attendanceService;
        _simulator = simulator;
    }

    /// <summary>Paginated, searchable, sortable attendance list. Admin/Manager see all; other roles are scoped client-side to their own EmployeeId.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAttendance([FromQuery] AttendanceQueryParams query, CancellationToken ct)
        => Ok(await _attendanceService.GetAttendanceAsync(query, ct));

    /// <summary>Self-service: any authenticated employee can view their own attendance.</summary>
    [HttpGet("my/{employeeId:int}")]
    public async Task<IActionResult> GetMyAttendance(int employeeId, [FromQuery] AttendanceQueryParams query, CancellationToken ct)
    {
        query.EmployeeId = employeeId;
        return Ok(await _attendanceService.GetAttendanceAsync(query, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _attendanceService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("summary/{year:int}/{month:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetMonthlySummary(int year, int month, [FromQuery] int? departmentId, CancellationToken ct)
        => Ok(await _attendanceService.GetMonthlySummaryAsync(year, month, departmentId, ct));

    /// <summary>Runs the rule engine for a given date, converting raw device punches into Attendance records.</summary>
    [HttpPost("process/{date:datetime}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ProcessForDate(DateTime date, CancellationToken ct)
    {
        var count = await _attendanceService.ProcessAttendanceForDateAsync(DateOnly.FromDateTime(date), ct);
        return Ok(new { processed = count, date = DateOnly.FromDateTime(date) });
    }

    [HttpPost("manual")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpsertManual([FromBody] ManualAttendanceDto dto, CancellationToken ct)
        => Ok(await _attendanceService.UpsertManualAttendanceAsync(dto, ct));

    /// <summary>Demo-only: generates fake device punches for the given date, standing in for real hardware.</summary>
    [HttpPost("simulate-device/{date:datetime}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SimulateDevice(DateTime date, CancellationToken ct)
    {
        var count = await _simulator.GenerateDemoPunchesAsync(DateOnly.FromDateTime(date), ct);
        return Ok(new { punchesGenerated = count });
    }
}
