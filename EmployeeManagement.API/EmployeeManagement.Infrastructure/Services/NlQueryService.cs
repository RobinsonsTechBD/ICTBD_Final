using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.DTOs.SqlAi;
using EmployeeManagement.Application.Interfaces;
using System.Diagnostics;
using System.Text;

namespace EmployeeManagement.Infrastructure.Services;

public class NlQueryService : INlQueryService
{
    private readonly ISchemaContextService _schemaContext;
    private readonly ISqlGenerationService _sqlGeneration;
    private readonly ISqlSafetyGuard _safetyGuard;
    private readonly ILiveQueryExecutor _queryExecutor;
    private readonly IModelFallbackChatService _chatService;

    public NlQueryService(
        ISchemaContextService schemaContext,
        ISqlGenerationService sqlGeneration,
        ISqlSafetyGuard safetyGuard,
        ILiveQueryExecutor queryExecutor,
        IModelFallbackChatService chatService)
    {
        _schemaContext = schemaContext;
        _sqlGeneration = sqlGeneration;
        _safetyGuard = safetyGuard;
        _queryExecutor = queryExecutor;
        _chatService = chatService;
    }

    public async Task<NlQueryResponseDto> QueryAsync(
        NlQueryRequestDto request, string userRole, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var maxRows = Math.Clamp(request.MaxRows, 1, 500);

        // 1. Get the role-filtered schema context
        var schema = _schemaContext.GetSchemaForRole(userRole);

        // 2. Generate SQL from natural language
        var (generatedSql, modelUsed) = await _sqlGeneration.GenerateAsync(request.Question, schema, ct);

        if (generatedSql.Contains("CANNOT_GENERATE", StringComparison.OrdinalIgnoreCase))
        {
            return new NlQueryResponseDto
            {
                Question = request.Question,
                GeneratedSql = generatedSql,
                IsSafe = false,
                SafetyRejectionReason = "The AI could not generate a query for this question using the available schema. Try rephrasing.",
                ModelUsed = modelUsed,
                ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2)
            };
        }

        // 3. Safety check — this is the critical gate before any DB execution
        var safetyResult = _safetyGuard.Check(generatedSql, maxRows);

        var response = new NlQueryResponseDto
        {
            Question = request.Question,
            GeneratedSql = safetyResult.CleanedSql ?? generatedSql,
            IsSafe = safetyResult.IsSafe,
            SafetyRejectionReason = safetyResult.RejectionReason,
            ModelUsed = modelUsed
        };

        if (!safetyResult.IsSafe || !request.ExecuteQuery)
        {
            response.WasExecuted = false;
            response.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2);
            return response;
        }

        // 4. Execute the validated query against the live database
        try
        {
            var (columns, rows) = await _queryExecutor.ExecuteAsync(safetyResult.CleanedSql!, ct);

            response.WasExecuted = true;
            response.Columns = columns;
            response.TotalRowCount = rows.Count;
            response.WasTruncated = rows.Count >= maxRows;
            response.Rows = rows;

            // 5. Generate a plain-English summary of the results
            if (rows.Count > 0)
                response.ResultSummary = await SummariseResultsAsync(request.Question, columns, rows, modelUsed, ct);
            else
                response.ResultSummary = "The query returned no results.";
        }
        catch (Exception ex)
        {
            response.WasExecuted = false;
            response.SafetyRejectionReason = $"Query execution failed: {ex.Message}. The generated SQL may have a syntax error — try rephrasing your question.";
        }

        response.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2);
        return response;
    }

    private async Task<string> SummariseResultsAsync(
        string question, List<string> columns, List<List<string?>> rows,
        string preferredModel, CancellationToken ct)
    {
        // Build a compact text table for the LLM to summarise (cap at 20 rows for the prompt)
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(" | ", columns));
        sb.AppendLine(new string('-', 60));
        foreach (var row in rows.Take(20))
            sb.AppendLine(string.Join(" | ", row.Select(v => v ?? "NULL")));

        if (rows.Count > 20)
            sb.AppendLine($"... and {rows.Count - 20} more rows");

        var summaryRequest = new ChatCompletionRequestDto
        {
            Temperature = 0.2,
            Messages = new List<ChatMessageDto>
            {
                new() { Role = "system", Content = "You are a concise data analyst. Summarise query results in 1-3 plain English sentences. No bullet points. Focus on the key insight that answers the original question." },
                new() { Role = "user", Content = $"Question: {question}\n\nResults:\n{sb}" }
            }
        };

        try
        {
            var summary = await _chatService.CompleteAsync(summaryRequest, ct);
            return summary.Content.Trim();
        }
        catch
        {
            return $"Query returned {rows.Count} row(s).";
        }
    }
}
