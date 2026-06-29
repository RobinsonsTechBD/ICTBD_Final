using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.DTOs.Common;
using EmployeeManagement.Application.DTOs.Employee;
using EmployeeManagement.Application.DTOs.SqlAi;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Services;

public class EmployeeService : IEmployeeService
{
    private readonly ApplicationDbContext _db;
    private readonly IModelFallbackChatService _chatService;
    private readonly INlQueryService _nlQueryService;

    public EmployeeService(
        ApplicationDbContext db,
        IModelFallbackChatService chatService,
        INlQueryService nlQueryService)
    {
        _db = db;
        _chatService = chatService;
        _nlQueryService = nlQueryService;
    }

    public async Task<PagedResult<EmployeeDto>> GetAllAsync(EmployeeQueryParams query, CancellationToken ct = default)
    {
        var q = _db.Employees
            .Include(e => e.Department)
            .Include(e => e.WorkShift)
            .AsNoTracking()
            .AsQueryable();

        if (query.DepartmentId.HasValue) q = q.Where(e => e.DepartmentId == query.DepartmentId);
        if (!string.IsNullOrWhiteSpace(query.Role)) q = q.Where(e => e.Role == query.Role);
        if (query.IsActive.HasValue) q = q.Where(e => e.IsActive == query.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(e => e.FullName.Contains(s) || e.Email.Contains(s) || e.EmployeeCode.Contains(s));
        }

        q = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("name", false) => q.OrderBy(e => e.FullName),
            ("name", true) => q.OrderByDescending(e => e.FullName),
            ("code", false) => q.OrderBy(e => e.EmployeeCode),
            ("department", false) => q.OrderBy(e => e.Department!.Name),
            (_, true) => q.OrderByDescending(e => e.CreatedAtUtc),
            _ => q.OrderBy(e => e.FullName)
        };

        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => MapToDto(e))
            .ToListAsync(ct);

        return new PagedResult<EmployeeDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var e = await _db.Employees
            .Include(x => x.Department)
            .Include(x => x.WorkShift)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : MapToDto(e);
    }

    /// <summary>
    /// AI touch #1 — after creating an employee, generate a personalised
    /// onboarding welcome summary the HR team can copy into a welcome email.
    /// </summary>
    public async Task<EmployeeOnboardingSummaryDto> CreateAsync(CreateEmployeeDto dto, CancellationToken ct = default)
    {
        if (await _db.Employees.AnyAsync(e => e.Email == dto.Email, ct))
            throw new InvalidOperationException($"An employee with email '{dto.Email}' already exists.");

        var dept = dto.DepartmentId.HasValue
            ? await _db.Departments.FindAsync(new object[] { dto.DepartmentId.Value }, ct)
            : null;

        var shift = dto.WorkShiftId.HasValue
            ? await _db.WorkShifts.FindAsync(new object[] { dto.WorkShiftId.Value }, ct)
            : null;

        // Generate employee code: EMP-{year}{month}{count+1}
        var count = await _db.Employees.CountAsync(ct);
        var code = $"EMP-{DateTime.UtcNow:yyMM}{(count + 1):D3}";

        var employee = new Employee
        {
            EmployeeCode = code,
            FullName = dto.FullName,
            Email = dto.Email,
            Phone = dto.Phone,
            Role = dto.Role,
            DepartmentId = dto.DepartmentId,
            WorkShiftId = dto.WorkShiftId,
            IsActive = true
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        // AI onboarding welcome summary
        var shiftInfo = shift is not null
            ? $"{shift.Name} ({shift.StartTime:hh\\:mm} – {shift.EndTime:hh\\:mm})"
            : "General Shift";

        var onboardingPrompt = $"""
            Write a warm, professional 2-paragraph onboarding welcome message for a new employee.
            Mention their name, role, department, and work schedule. Keep it concise and friendly.

            Employee Name: {dto.FullName}
            Role: {dto.Role}
            Department: {dept?.Name ?? "General"}
            Work Schedule: {shiftInfo}
            Start Date: {DateTime.UtcNow:dd MMMM yyyy}
            Company: Modhumoti Agro & Dairy Farm
            """;

        string welcomeSummary;
        string modelUsed;
        try
        {
            var completion = await _chatService.CompleteAsync(new ChatCompletionRequestDto
            {
                Temperature = 0.6,
                Messages = new List<ChatMessageDto>
                {
                    new() { Role = "system", Content = "You are an HR assistant writing professional onboarding welcome messages." },
                    new() { Role = "user", Content = onboardingPrompt }
                }
            }, ct);
            welcomeSummary = completion.Content.Trim();
            modelUsed = completion.ModelUsed;
        }
        catch
        {
            welcomeSummary = $"Welcome to Modhumoti Agro & Dairy Farm, {dto.FullName}! You have been registered as {dto.Role} in the {dept?.Name ?? "General"} department.";
            modelUsed = "fallback";
        }

        return new EmployeeOnboardingSummaryDto
        {
            EmployeeId = employee.Id,
            EmployeeCode = code,
            FullName = dto.FullName,
            AiWelcomeSummary = welcomeSummary,
            ModelUsed = modelUsed
        };
    }

    public async Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeDto dto, CancellationToken ct = default)
    {
        var entity = await _db.Employees.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Employee not found.");

        if (await _db.Employees.AnyAsync(e => e.Email == dto.Email && e.Id != id, ct))
            throw new InvalidOperationException($"Email '{dto.Email}' is already used by another employee.");

        entity.FullName = dto.FullName;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.Role = dto.Role;
        entity.DepartmentId = dto.DepartmentId;
        entity.WorkShiftId = dto.WorkShiftId;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task DeactivateAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Employees.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Employee not found.");
        entity.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// AI touch #2 — natural language employee search powered by the Phase G
    /// SQL AI module. Translates free-text questions into SQL, executes, then
    /// maps results back to EmployeeDto. Falls back to a basic name search
    /// if the SQL AI can't generate a valid query.
    /// </summary>
    public async Task<PagedResult<EmployeeDto>> NlSearchAsync(NlEmployeeSearchDto dto, CancellationToken ct = default)
    {
        // Route through the SQL AI module with execution enabled
        var nlResult = await _nlQueryService.QueryAsync(new NlQueryRequestDto
        {
            Question = $"Find employees: {dto.Question}. Return EmployeeId (as Id), EmployeeCode, FullName, Email, Role.",
            ExecuteQuery = true,
            MaxRows = 50
        }, "Admin", ct);

        if (!nlResult.WasExecuted || !nlResult.IsSafe || nlResult.Rows.Count == 0)
        {
            // Graceful fallback: basic text search
            var fallback = await GetAllAsync(new EmployeeQueryParams
            {
                Search = dto.Question,
                PageNumber = 1,
                PageSize = 20
            }, ct);
            return fallback;
        }

        // Map raw SQL columns back to EmployeeDtos
        var idCol = nlResult.Columns.FindIndex(c => c.Equals("Id", StringComparison.OrdinalIgnoreCase) || c.Equals("EmployeeId", StringComparison.OrdinalIgnoreCase));
        if (idCol == -1) return await GetAllAsync(new EmployeeQueryParams { Search = dto.Question, PageNumber = 1, PageSize = 20 }, ct);

        var ids = nlResult.Rows
            .Select(r => int.TryParse(r[idCol], out var i) ? i : 0)
            .Where(i => i > 0)
            .ToList();

        var employees = await _db.Employees
            .Include(e => e.Department).Include(e => e.WorkShift)
            .AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => MapToDto(e))
            .ToListAsync(ct);

        return new PagedResult<EmployeeDto>
        {
            Items = employees,
            TotalCount = employees.Count,
            PageNumber = 1,
            PageSize = employees.Count
        };
    }

    private static EmployeeDto MapToDto(Employee e) => new()
    {
        Id = e.Id,
        EmployeeCode = e.EmployeeCode,
        FullName = e.FullName,
        Email = e.Email,
        Phone = e.Phone,
        Role = e.Role,
        DepartmentId = e.DepartmentId,
        DepartmentName = e.Department?.Name,
        WorkShiftId = e.WorkShiftId,
        WorkShiftName = e.WorkShift?.Name,
        IsActive = e.IsActive,
        CreatedAtUtc = e.CreatedAtUtc
    };
}
