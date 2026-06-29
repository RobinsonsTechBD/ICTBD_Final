using EmployeeManagement.Application.DTOs.Common;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.DTOs.Attendance;

public class LeaveRequestDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = default!;
    public LeaveType LeaveType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TotalDays { get; set; }
    public string Reason { get; set; } = default!;
    public LeaveStatus Status { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ActionRemarks { get; set; }
}

public class CreateLeaveRequestDto
{
    public int EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = default!;
}

public class LeaveActionDto
{
    public LeaveStatus Status { get; set; }   // Approved / Rejected
    public string? ActionRemarks { get; set; }
}

public class LeaveQueryParams : QueryParams
{
    public int? EmployeeId { get; set; }
    public LeaveStatus? Status { get; set; }
}

public class HolidayDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public DateOnly Date { get; set; }
    public bool IsRecurringYearly { get; set; }
    public string? Description { get; set; }
}

public class WorkShiftDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GraceMinutes { get; set; }
    public bool IsActive { get; set; }
}
