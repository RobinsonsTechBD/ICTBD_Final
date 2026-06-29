using EmployeeManagement.Application.DTOs.Attendance;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Services;

public class HolidayService : IHolidayService
{
    private readonly ApplicationDbContext _db;
    public HolidayService(ApplicationDbContext db) => _db = db;

    public async Task<List<HolidayDto>> GetAllAsync(int? year, CancellationToken ct = default)
    {
        var q = _db.Holidays.AsNoTracking().AsQueryable();
        if (year.HasValue) q = q.Where(h => h.Date.Year == year || h.IsRecurringYearly);

        return await q.OrderBy(h => h.Date)
            .Select(h => new HolidayDto { Id = h.Id, Name = h.Name, Date = h.Date, IsRecurringYearly = h.IsRecurringYearly, Description = h.Description })
            .ToListAsync(ct);
    }

    public async Task<HolidayDto> CreateAsync(HolidayDto dto, CancellationToken ct = default)
    {
        var entity = new Holiday { Name = dto.Name, Date = dto.Date, IsRecurringYearly = dto.IsRecurringYearly, Description = dto.Description };
        _db.Holidays.Add(entity);
        await _db.SaveChangesAsync(ct);
        dto.Id = entity.Id;
        return dto;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Holidays.FindAsync(new object[] { id }, ct);
        if (entity is null) return;
        _db.Holidays.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}

public class ShiftService : IShiftService
{
    private readonly ApplicationDbContext _db;
    public ShiftService(ApplicationDbContext db) => _db = db;

    public async Task<List<WorkShiftDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.WorkShifts.AsNoTracking()
            .Select(s => new WorkShiftDto { Id = s.Id, Name = s.Name, StartTime = s.StartTime, EndTime = s.EndTime, GraceMinutes = s.GraceMinutes, IsActive = s.IsActive })
            .ToListAsync(ct);

    public async Task<WorkShiftDto> CreateAsync(WorkShiftDto dto, CancellationToken ct = default)
    {
        var entity = new WorkShift
        {
            Name = dto.Name,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            GraceMinutes = dto.GraceMinutes,
            IsActive = dto.IsActive
        };
        _db.WorkShifts.Add(entity);
        await _db.SaveChangesAsync(ct);
        dto.Id = entity.Id;
        return dto;
    }

    public async Task<WorkShiftDto> UpdateAsync(int id, WorkShiftDto dto, CancellationToken ct = default)
    {
        var entity = await _db.WorkShifts.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Shift not found.");
        entity.Name = dto.Name;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.GraceMinutes = dto.GraceMinutes;
        entity.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);
        return dto;
    }
}
