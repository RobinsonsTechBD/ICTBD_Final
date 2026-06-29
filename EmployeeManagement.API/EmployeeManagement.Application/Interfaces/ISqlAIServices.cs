using EmployeeManagement.Application.DTOs.SqlAi;

namespace EmployeeManagement.Application.Interfaces;

/// <summary>
/// Provides the LLM with a curated, role-filtered snapshot of the database
/// schema — only the tables/columns the current user's role is allowed to
/// query. This is the single most important safety layer in the whole module.
/// </summary>
public interface ISchemaContextService
{
    /// <summary>Returns a formatted schema description for injection into the SQL-generation prompt.</summary>
    string GetSchemaForRole(string role);
}

/// <summary>
/// Validates a generated SQL string before execution. Enforces:
/// - SELECT only (no INSERT/UPDATE/DELETE/DROP/EXEC/xp_/sp_)
/// - No multi-statement batches (no semicolons mid-query)
/// - No system tables (sys., INFORMATION_SCHEMA)
/// - No dynamic SQL constructs (EXEC, sp_executesql)
/// - Injects TOP {maxRows} if missing
/// </summary>
public interface ISqlSafetyGuard
{
    SqlSafetyCheckResult Check(string sql, int maxRows);
}

/// <summary>Generates SQL from a natural language question using the Phase B fallback chain.</summary>
public interface ISqlGenerationService
{
    Task<(string sql, string modelUsed)> GenerateAsync(string question, string schemaContext, CancellationToken ct = default);
}

/// <summary>
/// Executes a pre-validated SELECT query against the live database using a
/// read-only connection string, and returns results as a generic column/row
/// structure safe for JSON serialization.
/// </summary>
public interface ILiveQueryExecutor
{
    Task<(List<string> columns, List<List<string?>> rows)> ExecuteAsync(string sql, CancellationToken ct = default);
}

/// <summary>Orchestrates the full NL → SQL → execute → summarise pipeline.</summary>
public interface INlQueryService
{
    Task<NlQueryResponseDto> QueryAsync(NlQueryRequestDto request, string userRole, CancellationToken ct = default);
}