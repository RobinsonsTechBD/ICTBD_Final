using EmployeeManagement.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EmployeeManagement.Infrastructure.Services;

/// <summary>
/// Executes pre-validated SELECT queries using ADO.NET directly (not EF Core)
/// so we can use ApplicationIntent=ReadOnly on the connection string, and
/// avoid the overhead of materializing full entity objects for tabular results.
///
/// Uses a SEPARATE connection string key "ReadOnlyConnection" if present,
/// falling back to "DefaultConnection". In production, point "ReadOnlyConnection"
/// at a read replica or the same server with ApplicationIntent=ReadOnly to
/// ensure AI-generated queries can't affect transactional throughput.
/// </summary>
public class LiveQueryExecutor : ILiveQueryExecutor
{
    private readonly string _connectionString;

    public LiveQueryExecutor(IConfiguration config)
    {
        // Prefer a dedicated read-only connection string if configured
        _connectionString = config.GetConnectionString("ReadOnlyConnection")
            ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No connection string configured.");
    }

    public async Task<(List<string> columns, List<List<string?>> rows)> ExecuteAsync(
        string sql, CancellationToken ct = default)
    {
        var columns = new List<string>();
        var rows = new List<List<string?>>();

        // Use a 30-second command timeout — AI-generated queries can be slow on large tables
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = 30
        };

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        for (int i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));

        while (await reader.ReadAsync(ct))
        {
            var row = new List<string?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var val = reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
                row.Add(val);
            }
            rows.Add(row);
        }

        return (columns, rows);
    }
}