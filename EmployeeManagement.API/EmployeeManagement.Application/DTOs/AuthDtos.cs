namespace EmployeeManagement.Application.DTOs.Auth;

public class RegisterDto
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string Role { get; set; } = default!;     // "Admin","Manager","Marketing","Sales","Purchase","Delivery"
    public int? DepartmentId { get; set; }
}

public class LoginDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public class AuthResponseDto
{
    public string Token { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
}
