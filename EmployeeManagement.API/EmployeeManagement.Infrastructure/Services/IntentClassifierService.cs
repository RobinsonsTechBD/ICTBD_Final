using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.DTOs.Agent;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ChatMessageDto = EmployeeManagement.Application.DTOs.AI.ChatMessageDto;

namespace EmployeeManagement.Infrastructure.Services;

public class IntentClassifierService : IIntentClassifierService
{
    private readonly IModelFallbackChatService _chatService;
    private readonly ILogger<IntentClassifierService> _logger;

    private const string SystemPrompt = """
        You are an intent classifier for an HR system chatbot. Classify the user's
        message into EXACTLY ONE of these intents:

        - "GeneralChat": small talk, greetings, or anything not covered below
        - "DocumentQuestion": asking about contents of uploaded documents/policies/contracts
        - "AttendanceSummary": asking about their own attendance, lateness, absences, worked hours
        - "ApplyLeave": wants to request/apply for time off or leave
        - "CompanyHolidays": asking about upcoming holidays or off-days

        Respond with ONLY a JSON object, no other text, no markdown fences:
        {"intent": "<one of the values above>"}
        """;

    public IntentClassifierService(IModelFallbackChatService chatService, ILogger<IntentClassifierService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<IntentClassificationResult> ClassifyAsync(string userMessage, CancellationToken ct = default)
    {
        var request = new ChatCompletionRequestDto
        {
            Temperature = 0.0,   // deterministic classification, not creative
            Messages = new List<ChatMessageDto>
            {
                new() { Role = "system", Content = SystemPrompt },
                new() { Role = "user", Content = userMessage }
            }
        };

        try
        {
            var completion = await _chatService.CompleteAsync(request, ct);
            var intent = ParseIntent(completion.Content);
            return new IntentClassificationResult { Intent = intent };
        }
        catch (Exception ex)
        {
            // If classification itself fails (e.g. whole fallback chain down), degrade gracefully
            // to GeneralChat rather than failing the entire conversation.
            _logger.LogWarning(ex, "Intent classification failed, defaulting to GeneralChat.");
            return new IntentClassificationResult { Intent = AgentIntent.GeneralChat };
        }
    }

    private static AgentIntent ParseIntent(string rawContent)
    {
        // Small local models sometimes wrap JSON in markdown fences or add stray text —
        // extract the first {...} block defensively rather than trusting raw parse.
        var start = rawContent.IndexOf('{');
        var end = rawContent.LastIndexOf('}');
        if (start == -1 || end == -1 || end <= start)
            return AgentIntent.Unknown;

        try
        {
            var json = rawContent[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var intentString = doc.RootElement.GetProperty("intent").GetString() ?? "Unknown";

            return intentString switch
            {
                "GeneralChat" => AgentIntent.GeneralChat,
                "DocumentQuestion" => AgentIntent.DocumentQuestion,
                "AttendanceSummary" => AgentIntent.AttendanceSummary,
                "ApplyLeave" => AgentIntent.ApplyLeave,
                "CompanyHolidays" => AgentIntent.CompanyHolidays,
                _ => AgentIntent.Unknown
            };
        }
        catch (JsonException)
        {
            return AgentIntent.Unknown;
        }
    }
}
