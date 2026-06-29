namespace EmployeeManagement.Domain.Entities;

/// <summary>
/// Defines a working shift window (e.g. 10:00 - 19:00) and the grace period
/// after which a check-in is considered Late. Employees/Departments are
/// assigned a shift; default shift can be seeded as "General Shift".
/// </summary>
public class WorkShift
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;          // e.g. "General Shift"
    public TimeSpan StartTime { get; set; }                // 10:00:00
    public TimeSpan EndTime { get; set; }                  // 19:00:00
    public int GraceMinutes { get; set; } = 15;             // check-in allowed up to 10:15 without being "Late"
    public int HalfDayThresholdMinutes { get; set; } = 240; // worked < 4hrs => HalfDay
    public bool IsActive { get; set; } = true;

    public ICollection<Employee>? Employees { get; set; }
}
