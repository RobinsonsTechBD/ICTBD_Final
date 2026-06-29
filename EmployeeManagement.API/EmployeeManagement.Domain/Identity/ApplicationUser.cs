using Microsoft.AspNetCore.Identity;

namespace EmployeeManagement.Domain.Identity;

/// <summary>
/// Identity user. Kept separate from Employee on purpose — Identity owns
/// login/password/security-stamp concerns, Employee owns HR/business data.
/// EmployeeId links the two so JWTs can carry an "EmployeeId" claim, which
/// LeaveController and the attendance "my/{employeeId}" endpoints rely on.
/// </summary>
public class ApplicationUser : IdentityUser<int>
{
    public int? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}
