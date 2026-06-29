using EmployeeManagement.Application.DTOs.Auth;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Domain.Identity;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace EmployeeManagement.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _db;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        ITokenService tokenService,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _db = db;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            throw new InvalidOperationException("A user with this email already exists.");

        if (!await _roleManager.RoleExistsAsync(dto.Role))
            throw new InvalidOperationException($"Role '{dto.Role}' does not exist. Valid roles: Admin, Manager, Marketing, Sales, Purchase, Delivery.");

        // Create the HR-side Employee record first
        var employee = new Employee
        {
            EmployeeCode = $"EMP-{DateTime.UtcNow:yyyyMMddHHmmss}",
            FullName = dto.FullName,
            Email = dto.Email,
            Role = dto.Role,
            DepartmentId = dto.DepartmentId,
            IsActive = true
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);

        // Then the Identity login record, linked via EmployeeId
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            EmployeeId = employee.Id,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            // Roll back the Employee record if Identity creation fails (e.g. weak password)
            _db.Employees.Remove(employee);
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }

        await _userManager.AddToRoleAsync(user, dto.Role);

        var (token, expires) = _tokenService.GenerateToken(user, new List<string> { dto.Role }, employee.Id);

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAtUtc = expires,
            EmployeeId = employee.Id,
            FullName = employee.FullName,
            Email = employee.Email,
            Role = dto.Role
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("This account has been deactivated.");

        var validPassword = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!validPassword)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expires) = _tokenService.GenerateToken(user, roles, user.EmployeeId);

        var employee = user.EmployeeId.HasValue ? await _db.Employees.FindAsync(new object[] { user.EmployeeId.Value }, ct) : null;

        return new AuthResponseDto
        {
            Token = token,
            ExpiresAtUtc = expires,
            EmployeeId = user.EmployeeId ?? 0,
            FullName = employee?.FullName ?? user.UserName ?? "",
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? ""
        };
    }
}
