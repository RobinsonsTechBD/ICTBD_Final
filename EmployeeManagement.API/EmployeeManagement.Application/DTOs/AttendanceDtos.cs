using EmployeeManagement.Application.DTOs.Common;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.DTOs.Attendance;

public class AttendanceDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = default!;
    public string Department { get; set; } = default!;
    public DateOnly Date { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    public double? WorkedHours { get; set; }
    public AttendanceStatus Status { get; set; }
    public int? LateByMinutes { get; set; }
    public string? Remarks { get; set; }
}

public class AttendanceQueryParams : QueryParams
{
    public int? EmployeeId { get; set; }
    public int? DepartmentId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public AttendanceStatus? Status { get; set; }
}

/// <summary>Admin/Manager manual override or correction of a day's attendance.</summary>
public class ManualAttendanceDto
{
    public int EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
}

public class AttendanceSummaryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = default!;
    public int PresentDays { get; set; }
    public int LateDays { get; set; }
    public int AbsentDays { get; set; }
    public int LeaveDays { get; set; }
    public int HolidayDays { get; set; }
    public int OffDays { get; set; }
    public double TotalWorkedHours { get; set; }
}
