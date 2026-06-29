namespace EmployeeManagement.Application.DTOs.SqlAi;

public class NlQueryRequestDto
{
    /// <summary>Free-text question in plain English. e.g. "How many employees were late this month?"</summary>
    public string Question { get; set; } = default!;

    /// <summary>
    /// If true, executes the generated SQL and returns real data.
    /// If false, returns the SQL only for review — safe preview mode.
    /// </summary>
    public bool ExecuteQuery { get; set; } = false;

    /// <summary>Max rows returned to prevent accidental full-table dumps.</summary>
    public int MaxRows { get; set; } = 100;
}

public class NlQueryResponseDto
{
    public string Question { get; set; } = default!;
    public string GeneratedSql { get; set; } = default!;
    public bool WasExecuted { get; set; }
    public bool IsSafe { get; set; }
    public string? SafetyRejectionReason { get; set; }

    /// <summary>Column names in result order.</summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>Each row is a list of string-coerced cell values (safe for JSON serialization regardless of SQL type).</summary>
    public List<List<string?>> Rows { get; set; } = new();

    public int TotalRowCount { get; set; }
    public bool WasTruncated { get; set; }

    public string ModelUsed { get; set; } = default!;
    public double ElapsedSeconds { get; set; }

    /// <summary>Natural language summary of the result, generated after execution.</summary>
    public string? ResultSummary { get; set; }
}

public class SqlSafetyCheckResult
{
    public bool IsSafe { get; set; }
    public string? RejectionReason { get; set; }
    /// <summary>The cleaned SQL to actually execute (may have TOP/FETCH NEXT injected).</summary>
    public string? CleanedSql { get; set; }
}