using EmployeeManagement.Application.DTOs.Employee;
using EmployeeManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmployeeManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentController : ControllerBase
{
    private readonly IDepartmentService _departmentService;
    public DepartmentController(IDepartmentService departmentService) => _departmentService = departmentService;

    /// <summary>Get all active departments with employee count. Pass includeInactive=true to see deactivated ones.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await _departmentService.GetAllAsync(includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _departmentService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentDto dto, CancellationToken ct)
    {
        try { return Ok(await _departmentService.CreateAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDepartmentDto dto, CancellationToken ct)
    {
        try { return Ok(await _departmentService.UpdateAsync(id, dto, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>
    /// AI touch: returns today's attendance stats for the department plus
    /// an LLM-generated plain-English insight — great for a manager dashboard.
    /// </summary>
    [HttpGet("{id:int}/insight")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetInsight(int id, CancellationToken ct)
    {
        try { return Ok(await _departmentService.GetInsightAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    public EmployeeController(IEmployeeService employeeService) => _employeeService = employeeService;

    /// <summary>
    /// Paginated, searchable, sortable employee list.
    /// Params: pageNumber, pageSize, search, sortBy, sortDescending, departmentId, role, isActive.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetAll([FromQuery] EmployeeQueryParams query, CancellationToken ct)
        => Ok(await _employeeService.GetAllAsync(query, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _employeeService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Create a new employee. Returns the standard employee record PLUS
    /// an AI-generated onboarding welcome message (copy into welcome email).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto, CancellationToken ct)
    {
        try { return Ok(await _employeeService.CreateAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateEmployeeDto dto, CancellationToken ct)
    {
        try { return Ok(await _employeeService.UpdateAsync(id, dto, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Soft-deactivates an employee (IsActive = false). Never hard-deletes
    /// because attendance/leave history must be preserved.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        try { await _employeeService.DeactivateAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>
    /// AI touch: natural language employee search.
    /// e.g. "All Sales employees" or "Employees who joined this year"
    /// Routes through the SQL AI module — generates SQL, executes, maps results.
    /// Falls back to a basic name search if AI generation fails.
    /// </summary>
    [HttpPost("search/nl")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> NlSearch([FromBody] NlEmployeeSearchDto dto, CancellationToken ct)
    {
        try { return Ok(await _employeeService.NlSearchAsync(dto, ct)); }
        catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
    }
}
