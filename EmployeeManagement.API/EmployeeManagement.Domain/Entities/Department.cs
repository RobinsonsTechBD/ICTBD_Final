namespace EmployeeManagement.Domain.Entities;

/// <summary>
/// Minimal Department entity. Same note as Employee.cs — if you already have
/// one, skip this file and just make sure yours is named "Department" with
/// an "Id" and "Name" property so the Attendance module's references resolve.
/// </summary>
public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;   // "Marketing","Sales","Purchase","Delivery", etc.
    public bool IsActive { get; set; } = true;

    public ICollection<Employee>? Employees { get; set; }
}
