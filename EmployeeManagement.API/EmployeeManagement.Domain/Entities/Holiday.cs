namespace EmployeeManagement.Domain.Entities;

public class Holiday
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;     // "Eid-ul-Fitr", "Independence Day"
    public DateOnly Date { get; set; }
    public bool IsRecurringYearly { get; set; }        // e.g. national holidays repeat same month/day
    public string? Description { get; set; }
}
