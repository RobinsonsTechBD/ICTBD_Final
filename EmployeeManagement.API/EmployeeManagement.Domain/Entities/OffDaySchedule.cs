namespace EmployeeManagement.Domain.Entities;

/// <summary>
/// Weekly recurring off-day (e.g. Friday) for an employee or, if EmployeeId is null,
/// applied at DepartmentId level as a default for all employees in that department.
/// </summary>
public class OffDaySchedule
{
    public int Id { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public bool IsActive { get; set; } = true;
}
