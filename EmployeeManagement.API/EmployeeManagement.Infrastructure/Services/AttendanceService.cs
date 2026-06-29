using EmployeeManagement.Application.DTOs.Attendance;
using EmployeeManagement.Application.DTOs.Common;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Services;

public class AttendanceService : IAttendanceService
{
    private readonly ApplicationDbContext _db;

    public AttendanceService(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<AttendanceDto>> GetAttendanceAsync(AttendanceQueryParams query, CancellationToken ct = default)
    {
        var q = _db.Attendances
            .Include(a => a.Employee).ThenInclude(e => e.Department)
            .AsNoTracking()
            .AsQueryable();

        if (query.EmployeeId.HasValue) q = q.Where(a => a.EmployeeId == query.EmployeeId);
        if (query.DepartmentId.HasValue) q = q.Where(a => a.Employee.DepartmentId == query.DepartmentId);
        if (query.FromDate.HasValue) q = q.Where(a => a.Date >= query.FromDate);
        if (query.ToDate.HasValue) q = q.Where(a => a.Date <= query.ToDate);
        if (query.Status.HasValue) q = q.Where(a => a.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(a => a.Employee.FullName.Contains(s) || a.Employee.EmployeeCode.Contains(s));
        }

        q = (query.SortBy?.ToLower(), query.SortDescending) switch
        {
            ("name", false) => q.OrderBy(a => a.Employee.FullName),
            ("name", true) => q.OrderByDescending(a => a.Employee.FullName),
            ("status", false) => q.OrderBy(a => a.Status),
            ("status", true) => q.OrderByDescending(a => a.Status),
            (_, true) => q.OrderByDescending(a => a.Date),
            _ => q.OrderBy(a => a.Date)
        };

        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(ct);

        return new PagedResult<AttendanceDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<AttendanceDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var a = await _db.Attendances.Include(x => x.Employee).ThenInclude(e => e.Department)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? null : MapToDto(a);
    }

    public async Task<List<AttendanceSummaryDto>> GetMonthlySummaryAsync(int year, int month, int? departmentId, CancellationToken ct = default)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var q = _db.Attendances
            .Include(a => a.Employee)
            .Where(a => a.Date >= from && a.Date <= to);

        if (departmentId.HasValue) q = q.Where(a => a.Employee.DepartmentId == departmentId);

        var grouped = await q
            .GroupBy(a => new { a.EmployeeId, a.Employee.FullName })
            .Select(g => new AttendanceSummaryDto
            {
                EmployeeId = g.Key.EmployeeId,
                EmployeeName = g.Key.FullName,
                PresentDays = g.Count(x => x.Status == AttendanceStatus.Present),
                LateDays = g.Count(x => x.Status == AttendanceStatus.Late),
                AbsentDays = g.Count(x => x.Status == AttendanceStatus.Absent),
                LeaveDays = g.Count(x => x.Status == AttendanceStatus.OnLeave),
                HolidayDays = g.Count(x => x.Status == AttendanceStatus.Holiday),
                OffDays = g.Count(x => x.Status == AttendanceStatus.OffDay),
                TotalWorkedHours = g.Sum(x => x.WorkedHours ?? 0)
            })
            .OrderBy(x => x.EmployeeName)
            .ToListAsync(ct);

        return grouped;
    }

    /// <summary>
    /// THE CORE RULE ENGINE.
    /// Priority order for determining a day's status:
    /// 1. Holiday (company-wide)               -> Status = Holiday
    /// 2. Employee's weekly OffDay              -> Status = OffDay
    /// 3. Approved LeaveRequest covering date    -> Status = OnLeave
    /// 4. No CheckIn punch found                -> Status = Absent
    /// 5. CheckIn within shift.GraceMinutes      -> Status = Present
    /// 6. CheckIn after grace                    -> Status = Late (LateByMinutes computed)
    /// 7. WorkedHours < shift.HalfDayThreshold   -> downgraded to HalfDay (kept as Remarks note)
    /// </summary>
    public async Task<int> ProcessAttendanceForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var shift = await _db.WorkShifts.AsNoTracking().FirstOrDefaultAsync(s => s.IsActive, ct)
                    ?? throw new InvalidOperationException("No active WorkShift configured.");

        var holiday = await _db.Holidays.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Date == date || (h.IsRecurringYearly && h.Date.Month == date.Month && h.Date.Day == date.Day), ct);

        var employees = await _db.Employees.AsNoTracking().Where(e => e.IsActive).ToListAsync(ct);
        var offDayMap = await _db.OffDaySchedules.AsNoTracking()
            .Where(o => o.IsActive && o.DayOfWeek == date.DayOfWeek)
            .ToListAsync(ct);

        var approvedLeaves = await _db.LeaveRequests.AsNoTracking()
            .Where(l => l.Status == LeaveStatus.Approved && l.StartDate <= date && l.EndDate >= date)
            .ToListAsync(ct);

        var punches = await _db.AttendanceDeviceLogs.AsNoTracking()
            .Where(p => p.PunchTimeUtc.Date == date.ToDateTime(TimeOnly.MinValue).Date)
            .ToListAsync(ct);

        int processedCount = 0;

        foreach (var emp in employees)
        {
            var existing = await _db.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == emp.Id && a.Date == date, ct);
            // Manual entries are never overwritten by the automated engine
            if (existing is not null && existing.Source == DataSource.ManualByAdmin) continue;

            var record = existing ?? new Attendance { EmployeeId = emp.Id, Date = date };

            if (holiday is not null)
            {
                record.Status = AttendanceStatus.Holiday;
                record.Remarks = holiday.Name;
            }
            else if (offDayMap.Any(o => o.EmployeeId == emp.Id || o.DepartmentId == emp.DepartmentId))
            {
                record.Status = AttendanceStatus.OffDay;
            }
            else if (approvedLeaves.FirstOrDefault(l => l.EmployeeId == emp.Id) is { } leave)
            {
                record.Status = AttendanceStatus.OnLeave;
                record.LeaveRequestId = leave.Id;
            }
            else
            {
                var empPunches = punches.Where(p => p.EmployeeId == emp.Id).OrderBy(p => p.PunchTimeUtc).ToList();
                var checkIn = empPunches.FirstOrDefault(p => p.PunchType == PunchType.CheckIn);
                var checkOut = empPunches.LastOrDefault(p => p.PunchType == PunchType.CheckOut);

                if (checkIn is null)
                {
                    record.Status = AttendanceStatus.Absent;
                    record.CheckInTime = null;
                    record.CheckOutTime = null;
                    record.WorkedHours = null;
                }
                else
                {
                    var checkInTime = TimeOnly.FromDateTime(checkIn.PunchTimeUtc).ToTimeSpan();
                    record.CheckInTime = checkInTime;

                    var graceDeadline = shift.StartTime.Add(TimeSpan.FromMinutes(shift.GraceMinutes));
                    if (checkInTime <= graceDeadline)
                    {
                        record.Status = AttendanceStatus.Present;
                        record.LateByMinutes = null;
                    }
                    else
                    {
                        record.Status = AttendanceStatus.Late;
                        record.LateByMinutes = (int)(checkInTime - shift.StartTime).TotalMinutes;
                    }

                    if (checkOut is not null)
                    {
                        var checkOutTime = TimeOnly.FromDateTime(checkOut.PunchTimeUtc).ToTimeSpan();
                        record.CheckOutTime = checkOutTime;
                        var worked = (checkOutTime - checkInTime).TotalHours;
                        record.WorkedHours = worked > 0 ? Math.Round(worked, 2) : 0;

                        if (worked * 60 < shift.HalfDayThresholdMinutes)
                            record.Remarks = "Worked hours below half-day threshold";
                    }
                }
            }

            record.Source = DataSource.Device;
            record.ProcessedAtUtc = DateTime.UtcNow;

            if (existing is null) _db.Attendances.Add(record);
            processedCount++;
        }

        await _db.SaveChangesAsync(ct);
        return processedCount;
    }

    public async Task<AttendanceDto> UpsertManualAttendanceAsync(ManualAttendanceDto dto, CancellationToken ct = default)
    {
        var record = await _db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == dto.EmployeeId && a.Date == dto.Date, ct)
            ?? new Attendance { EmployeeId = dto.EmployeeId, Date = dto.Date };

        record.CheckInTime = dto.CheckInTime;
        record.CheckOutTime = dto.CheckOutTime;
        record.Status = dto.Status;
        record.Remarks = dto.Remarks;
        record.Source = DataSource.ManualByAdmin;
        record.ProcessedAtUtc = DateTime.UtcNow;

        if (dto.CheckInTime.HasValue && dto.CheckOutTime.HasValue)
            record.WorkedHours = Math.Round((dto.CheckOutTime.Value - dto.CheckInTime.Value).TotalHours, 2);

        if (record.Id == 0) _db.Attendances.Add(record);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(record.Id, ct))!;
    }

    private static AttendanceDto MapToDto(Attendance a) => new()
    {
        Id = a.Id,
        EmployeeId = a.EmployeeId,
        EmployeeName = a.Employee.FullName,
        Department = a.Employee.Department != null ? a.Employee.Department.Name : "",
        Date = a.Date,
        CheckInTime = a.CheckInTime,
        CheckOutTime = a.CheckOutTime,
        WorkedHours = a.WorkedHours,
        Status = a.Status,
        LateByMinutes = a.LateByMinutes,
        Remarks = a.Remarks
    };
}
