namespace EmployeeManagement.Domain.Entities;

public enum AgentIntent
{
    GeneralChat = 1,        // small talk / anything that doesn't need a tool
    DocumentQuestion = 2,   // routes to Phase C's RAG pipeline
    AttendanceSummary = 3,  // "how many days have I been late this month?"
    ApplyLeave = 4,         // "I want to take leave next Monday"
    CompanyHolidays = 5,    // "when is the next holiday?"
    Unknown = 6
}

/// <summary>One chat thread per employee. Keeps conversational history for context.</summary>
public class ChatSession
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public string Title { get; set; } = "New Conversation";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// One turn in a session. Intent/ToolUsed are recorded even for the user's own
/// messages' resulting assistant reply, so the conversation log doubles as an
/// audit trail of what the agent actually did (useful for the course writeup
/// and for debugging misrouted intents).
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public ChatSession Session { get; set; } = default!;

    public string Role { get; set; } = default!;       // "user" | "assistant"
    public string Content { get; set; } = default!;

    public AgentIntent? DetectedIntent { get; set; }    // null for user messages, set for the assistant's handling of them
    public string? ToolUsed { get; set; }                // e.g. "AttendanceService.GetMonthlySummaryAsync"

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
