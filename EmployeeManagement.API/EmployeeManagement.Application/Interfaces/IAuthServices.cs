using EmployeeManagement.Application.DTOs.Auth;
using EmployeeManagement.Domain.Identity;

namespace EmployeeManagement.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
}

/// <summary>Implemented in Infrastructure since it deals with signing keys / config.</summary>
public interface ITokenService
{
    (string token, DateTime expiresAtUtc) GenerateToken(ApplicationUser user, IList<string> roles, int? employeeId);
}
