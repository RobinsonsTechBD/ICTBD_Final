using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.DTOs.Employee;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Services;

public class DepartmentService : IDepartmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IModelFallbackChatService _chatService;

    public DepartmentService(ApplicationDbContext db, IModelFallbackChatService chatService)
    {
        _db = db;
        _chatService = chatService;
    }

    public async Task<List<DepartmentDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var q = _db.Departments.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(d => d.IsActive);

        return await q.OrderBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                IsActive = d.IsActive,
                EmployeeCount = d.Employees!.Count(e => e.IsActive)
            })
            .ToListAsync(ct);
    }

    public async Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var d = await _db.Departments.AsNoTracking()
            .Include(x => x.Employees)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (d is null) return null;
        return new DepartmentDto
        {
            Id = d.Id,
            Name = d.Name,
            IsActive = d.IsActive,
            EmployeeCount = d.Employees?.Count(e => e.IsActive) ?? 0
        };
    }

    public async Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken ct = default)
    {
        if (await _db.Departments.AnyAsync(d => d.Name == dto.Name, ct))
            throw new InvalidOperationException($"Department '{dto.Name}' already exists.");

        var entity = new Department { Name = dto.Name, IsActive = true };
        _db.Departments.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new DepartmentDto { Id = entity.Id, Name = entity.Name, IsActive = true, EmployeeCount = 0 };
    }

    public async Task<DepartmentDto> UpdateAsync(int id, UpdateDepartmentDto dto, CancellationToken ct = default)
    {
        var entity = await _db.Departments.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Department not found.");

        entity.Name = dto.Name;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    /// <summary>
    /// AI touch: pulls today's attendance stats for the department, then asks
    /// the LLM to summarise the situation in 2-3 readable sentences.
    /// Useful for a manager's daily dashboard widget.
    /// </summary>
    public async Task<DepartmentInsightDto> GetInsightAsync(int id, CancellationToken ct = default)
    {
        var dept = await _db.Departments.AsNoTracking()
            .Include(d => d.Employees)
            .FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException("Department not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var employeeIds = dept.Employees!.Where(e => e.IsActive).Select(e => e.Id).ToList();

        var todayAttendance = await _db.Attendances.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.Date == today)
            .ToListAsync(ct);

        var insight = new DepartmentInsightDto
        {
            DepartmentId = dept.Id,
            DepartmentName = dept.Name,
            TotalEmployees = employeeIds.Count,
            ActiveEmployees = employeeIds.Count,
            PresentToday = todayAttendance.Count(a => a.Status == AttendanceStatus.Present),
            LateToday = todayAttendance.Count(a => a.Status == AttendanceStatus.Late),
            AbsentToday = todayAttendance.Count(a => a.Status == AttendanceStatus.Absent)
        };

        // AI-generated plain-English insight
        var prompt = $"""
            You are an HR assistant. Summarise this department's attendance in 2-3 sentences.
            Be direct and professional. Note anything that needs attention.

            Department: {dept.Name}
            Total employees: {insight.TotalEmployees}
            Present today: {insight.PresentToday}
            Late today: {insight.LateToday}
            Absent today: {insight.AbsentToday}
            No data yet: {insight.TotalEmployees - todayAttendance.Count}
            """;

        try
        {
            var completion = await _chatService.CompleteAsync(new ChatCompletionRequestDto
            {
                Temperature = 0.3,
                Messages = new List<ChatMessageDto>
                {
                    new() { Role = "system", Content = "You are a concise HR reporting assistant. Keep summaries under 3 sentences." },
                    new() { Role = "user", Content = prompt }
                }
            }, ct);
            insight.AiInsightSummary = completion.Content.Trim();
            insight.ModelUsed = completion.ModelUsed;
        }
        catch
        {
            insight.AiInsightSummary = $"{dept.Name} has {insight.PresentToday} present, {insight.LateToday} late, and {insight.AbsentToday} absent today.";
            insight.ModelUsed = "fallback";
        }

        return insight;
    }
}
