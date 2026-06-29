using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.Interfaces;
using System.Text.RegularExpressions;

namespace EmployeeManagement.Infrastructure.Services;

public class SqlGenerationService : ISqlGenerationService
{
    private readonly IModelFallbackChatService _chatService;

    public SqlGenerationService(IModelFallbackChatService chatService)
        => _chatService = chatService;

    public async Task<(string sql, string modelUsed)> GenerateAsync(
        string question, string schemaContext, CancellationToken ct = default)
    {
        var systemPrompt = $"""
            You are an expert MS SQL Server query generator for an Employee Management system.
            You will be given a database schema and a question in plain English.
            Your response must be ONLY a valid SQL SELECT query — no explanation, no markdown fences, no preamble.

            Rules:
            - Generate only SELECT statements
            - Never use INSERT, UPDATE, DELETE, DROP, EXEC, or any DDL
            - Use proper MS SQL Server syntax (TOP instead of LIMIT, GETDATE() instead of NOW())
            - Use table aliases for readability
            - For Status/LeaveType/Provider enum columns, use the numeric values defined in the schema comments
            - Always qualify column names with table alias when joining multiple tables
            - If the question cannot be answered with the available schema, respond with exactly: CANNOT_GENERATE

            DATABASE SCHEMA:
            {schemaContext}
            """;

        var request = new ChatCompletionRequestDto
        {
            Temperature = 0.0,    // deterministic — SQL generation is not creative
            Messages = new List<ChatMessageDto>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = question }
            }
        };

        var completion = await _chatService.CompleteAsync(request, ct);
        var raw = completion.Content.Trim();

        // Strip markdown fences if the model wrapped its output despite instructions
        var sql = StripMarkdownFences(raw);

        return (sql, completion.ModelUsed);
    }

    private static string StripMarkdownFences(string raw)
    {
        // Remove ```sql ... ``` or ``` ... ```
        var stripped = Regex.Replace(raw, @"^```[a-zA-Z]*\s*", "", RegexOptions.Multiline).Trim();
        stripped = Regex.Replace(stripped, @"```\s*$", "", RegexOptions.Multiline).Trim();
        return stripped;
    }
}
