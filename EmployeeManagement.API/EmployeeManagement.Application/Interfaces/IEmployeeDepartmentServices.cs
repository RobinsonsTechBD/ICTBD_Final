using EmployeeManagement.Application.DTOs.Common;
using EmployeeManagement.Application.DTOs.Employee;

namespace EmployeeManagement.Application.Interfaces;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<DepartmentDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, CancellationToken ct = default);
    Task<DepartmentDto> UpdateAsync(int id, UpdateDepartmentDto dto, CancellationToken ct = default);
    Task<DepartmentInsightDto> GetInsightAsync(int id, CancellationToken ct = default);
}

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> GetAllAsync(EmployeeQueryParams query, CancellationToken ct = default);
    Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<EmployeeOnboardingSummaryDto> CreateAsync(CreateEmployeeDto dto, CancellationToken ct = default);
    Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeDto dto, CancellationToken ct = default);
    Task DeactivateAsync(int id, CancellationToken ct = default);
    Task<PagedResult<EmployeeDto>> NlSearchAsync(NlEmployeeSearchDto dto, CancellationToken ct = default);
}