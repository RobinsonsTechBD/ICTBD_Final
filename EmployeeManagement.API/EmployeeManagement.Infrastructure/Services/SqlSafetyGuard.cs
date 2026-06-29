using EmployeeManagement.Application.DTOs.SqlAi;
using EmployeeManagement.Application.Interfaces;
using System.Text.RegularExpressions;

namespace EmployeeManagement.Infrastructure.Services;

/// <summary>
/// Defense-in-depth SQL safety layer. Even if the LLM produces malicious SQL
/// (prompt injection, jailbreak, hallucinated table names) this guard ensures
/// nothing destructive reaches the database.
///
/// Strategy: allowlist approach — only a SELECT at the outermost level is
/// permitted, everything else is rejected.
/// </summary>
public class SqlSafetyGuard : ISqlSafetyGuard
{
    // Forbidden keywords that must not appear anywhere in the SQL
    private static readonly string[] ForbiddenKeywords =
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE",
        "EXEC", "EXECUTE", "SP_", "XP_", "OPENROWSET", "OPENDATASOURCE",
        "BULK INSERT", "INTO", "MERGE", "GRANT", "REVOKE", "DENY"
    };

    // Tables that must never be queried (contain passwords, tokens, security stamps)
    private static readonly string[] ForbiddenTables =
    {
        "ASPNETUSERS", "ASPNETUSERROLES", "ASPNETUSERLOGINS",
        "ASPNETUSERCLAIMSASPNETUSERTOKENS", "ASPNETROLES",
        "DOCUMENTCHUNKS",   // contains raw embedding vectors — too large and meaningless for tabular results
        "__EFMIGRATIONSHISTORY"
    };

    public SqlSafetyCheckResult Check(string sql, int maxRows)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return Fail("Generated SQL is empty.");

        var normalized = Regex.Replace(sql.ToUpperInvariant(), @"\s+", " ").Trim();

        // Must start with SELECT (after stripping optional WITH for CTEs)
        var withoutCte = Regex.Replace(normalized, @"^WITH\s+\w+\s+AS\s*\(.*?\)\s*", "", RegexOptions.Singleline).Trim();
        if (!withoutCte.StartsWith("SELECT"))
            return Fail("Only SELECT queries are permitted. Generated SQL does not start with SELECT.");

        // Check forbidden keywords
        foreach (var keyword in ForbiddenKeywords)
        {
            // Word-boundary check so e.g. "INSERTED" doesn't match "INSERT"
            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(keyword)}\b"))
                return Fail($"Forbidden keyword '{keyword}' detected in generated SQL.");
        }

        // Check forbidden tables
        foreach (var table in ForbiddenTables)
        {
            if (normalized.Contains(table))
                return Fail($"Access to table '{table}' is not permitted via AI SQL generation.");
        }

        // No semicolons mid-query (prevents statement stacking)
        var strippedOfStrings = Regex.Replace(sql, @"'[^']*'", "''");
        if (strippedOfStrings.Count(c => c == ';') > 1 ||
            (strippedOfStrings.Contains(';') && !strippedOfStrings.TrimEnd().EndsWith(';')))
            return Fail("Multi-statement SQL is not permitted.");

        // Inject TOP {maxRows} if not already present and no ORDER BY + FETCH NEXT
        var cleanedSql = InjectRowLimit(sql, maxRows);

        return new SqlSafetyCheckResult { IsSafe = true, CleanedSql = cleanedSql };
    }

    private static string InjectRowLimit(string sql, int maxRows)
    {
        var upper = sql.ToUpperInvariant();

        // Already has TOP or FETCH NEXT — don't double-inject
        if (Regex.IsMatch(upper, @"\bTOP\s*\(?\s*\d+") || upper.Contains("FETCH NEXT"))
            return sql;

        // Inject TOP after SELECT (handles SELECT DISTINCT too)
        return Regex.Replace(sql, @"(?i)\bSELECT\b(\s+DISTINCT\b)?", $"SELECT$1 TOP {maxRows}", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
    }

    private static SqlSafetyCheckResult Fail(string reason) =>
        new() { IsSafe = false, RejectionReason = reason };
}