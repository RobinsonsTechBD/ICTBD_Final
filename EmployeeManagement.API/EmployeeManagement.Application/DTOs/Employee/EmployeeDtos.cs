using EmployeeManagement.Application.DTOs.Common;

namespace EmployeeManagement.Application.DTOs.Employee;

// ---- Department DTOs ----

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }
}

public class CreateDepartmentDto
{
    public string Name { get; set; } = default!;
}

public class UpdateDepartmentDto
{
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; }
}

// ---- Employee DTOs ----

public class EmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string Role { get; set; } = default!;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? WorkShiftId { get; set; }
    public string? WorkShiftName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateEmployeeDto
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string Role { get; set; } = default!;
    public int? DepartmentId { get; set; }
    public int? WorkShiftId { get; set; }
}

public class UpdateEmployeeDto
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string Role { get; set; } = default!;
    public int? DepartmentId { get; set; }
    public int? WorkShiftId { get; set; }
    public bool IsActive { get; set; }
}

public class EmployeeQueryParams : QueryParams
{
    public int? DepartmentId { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

// ---- AI-touched DTOs ----

public class EmployeeOnboardingSummaryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string AiWelcomeSummary { get; set; } = default!;
    public string ModelUsed { get; set; } = default!;
}

public class DepartmentInsightDto
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = default!;
    public int TotalEmployees { get; set; }
    public int ActiveEmployees { get; set; }
    public int PresentToday { get; set; }
    public int LateToday { get; set; }
    public int AbsentToday { get; set; }
    public string AiInsightSummary { get; set; } = default!;
    public string ModelUsed { get; set; } = default!;
}

public class NlEmployeeSearchDto
{
    /// <summary>e.g. "Show me all Sales employees who joined this year" or "Who is in the Delivery department?"</summary>
    public string Question { get; set; } = default!;
}
