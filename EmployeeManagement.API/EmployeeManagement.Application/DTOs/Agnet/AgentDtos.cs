using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.DTOs.Agent;

public class SendMessageDto
{
    public int? SessionId { get; set; }   // null = start a new session
    public string Message { get; set; } = default!;
}

public class AgentResponseDto
{
    public int SessionId { get; set; }
    public string Reply { get; set; } = default!;
    public AgentIntent Intent { get; set; }
    public string? ToolUsed { get; set; }
    public string? ModelUsed { get; set; }
    /// <summary>Populated only when Intent == DocumentQuestion, mirrors RagAnswerDto.Sources.</summary>
    public List<object>? Sources { get; set; }
}

public class ChatSessionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public string Role { get; set; } = default!;
    public string Content { get; set; } = default!;
    public AgentIntent? DetectedIntent { get; set; }
    public string? ToolUsed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Internal result of intent classification — not exposed directly via API.</summary>
public class IntentClassificationResult
{
    public AgentIntent Intent { get; set; } = AgentIntent.Unknown;
    public Dictionary<string, string> ExtractedParameters { get; set; } = new();
}

/// <summary>Internal result of extracting leave-application fields from free text.</summary>
public class LeaveExtractionResult
{
    public bool IsComplete { get; set; }
    public string? LeaveType { get; set; }
    public string? StartDate { get; set; }   // ISO yyyy-MM-dd, as a string since LLM extraction isn't always parseable directly
    public string? EndDate { get; set; }
    public string? Reason { get; set; }
    public string? MissingFieldsMessage { get; set; }   // what to ask the user if IsComplete == false
}
