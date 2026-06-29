using EmployeeManagement.Application.DTOs.Attendance;
using EmployeeManagement.Application.DTOs.Common;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.Interfaces;

public interface IAttendanceService
{
    Task<PagedResult<AttendanceDto>> GetAttendanceAsync(AttendanceQueryParams query, CancellationToken ct = default);
    Task<AttendanceDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<AttendanceSummaryDto>> GetMonthlySummaryAsync(int year, int month, int? departmentId, CancellationToken ct = default);

    /// <summary>
    /// Core engine: processes raw AttendanceDeviceLog rows for a given date
    /// into aggregated Attendance rows, applying Late/Absent/Leave/Holiday/OffDay rules.
    /// Idempotent — safe to re-run for the same date (e.g. nightly job or manual trigger).
    /// </summary>
    Task<int> ProcessAttendanceForDateAsync(DateOnly date, CancellationToken ct = default);

    Task<AttendanceDto> UpsertManualAttendanceAsync(ManualAttendanceDto dto, CancellationToken ct = default);
}

public interface ILeaveService
{
    Task<PagedResult<LeaveRequestDto>> GetLeaveRequestsAsync(LeaveQueryParams query, CancellationToken ct = default);
    Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto dto, CancellationToken ct = default);
    Task<LeaveRequestDto> ActionAsync(int leaveRequestId, int actionedByEmployeeId, LeaveActionDto dto, CancellationToken ct = default);
}

public interface IHolidayService
{
    Task<List<HolidayDto>> GetAllAsync(int? year, CancellationToken ct = default);
    Task<HolidayDto> CreateAsync(HolidayDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IShiftService
{
    Task<List<WorkShiftDto>> GetAllAsync(CancellationToken ct = default);
    Task<WorkShiftDto> CreateAsync(WorkShiftDto dto, CancellationToken ct = default);
    Task<WorkShiftDto> UpdateAsync(int id, WorkShiftDto dto, CancellationToken ct = default);
}

public interface IAttendanceDeviceSimulatorService
{
    /// <summary>
    /// Generates realistic demo punch logs for all active employees for the given date
    /// (random check-in spread around shift start, occasional lateness, occasional missed punch = Absent).
    /// </summary>
    Task<int> GenerateDemoPunchesAsync(DateOnly date, CancellationToken ct = default);
}
