namespace EmployeeManagement.Domain.Entities;

/// <summary>
/// One row per employee per calendar date. Built by AttendanceService from
/// AttendanceDeviceLog punches, cross-checked against LeaveRequest, Holiday
/// and OffDaySchedule. This is the table all reports/queries/AI SQL generation
/// will read from — never read raw device logs directly for reporting.
/// </summary>
public class Attendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public DateOnly Date { get; set; }
    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    public double? WorkedHours { get; set; }

    public AttendanceStatus Status { get; set; }
    public int? LateByMinutes { get; set; }
    public string? Remarks { get; set; }

    public int? LeaveRequestId { get; set; }     // set when Status == OnLeave
    public LeaveRequest? LeaveRequest { get; set; }

    public DataSource Source { get; set; } = DataSource.Device;
    public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
}
