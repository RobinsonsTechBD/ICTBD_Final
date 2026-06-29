namespace EmployeeManagement.Domain.Entities;

public class LeaveRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public LeaveType LeaveType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = default!;

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public int? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedByEmployee { get; set; }
    public DateTime? ActionedAtUtc { get; set; }
    public string? ActionRemarks { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public int TotalDays => EndDate.DayNumber - StartDate.DayNumber + 1;
}
