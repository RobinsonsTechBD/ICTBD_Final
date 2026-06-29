namespace EmployeeManagement.Domain.Entities;

/// <summary>
/// Minimal Employee entity. If you already built a fuller version in an
/// earlier phase (with ApplicationUserId link to Identity, hire date, salary,
/// etc.), DO NOT use this file — instead just add the two fields marked
/// below (DepartmentId/Department and WorkShiftId/WorkShift) to your real one.
/// </summary>
public class Employee
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = default!;   // e.g. "EMP-0001"
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string Role { get; set; } = default!;            // "Admin","Manager","Marketing","Sales","Purchase","Delivery"
    public bool IsActive { get; set; } = true;

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Required by WorkShift.cs / AttendanceService for the 10AM-7PM rule engine
    public int? WorkShiftId { get; set; }
    public WorkShift? WorkShift { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}