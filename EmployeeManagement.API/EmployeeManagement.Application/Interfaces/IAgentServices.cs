using EmployeeManagement.Application.DTOs.Agent;

namespace EmployeeManagement.Application.Interfaces;

public interface IIntentClassifierService
{
    Task<IntentClassificationResult> ClassifyAsync(string userMessage, CancellationToken ct = default);
}

/// <summary>
/// The agent "brain": classifies intent, routes to the right internal tool
/// (Attendance/Leave/Holiday/RAG services), and composes the final reply.
/// Tool routing happens in C# rather than relying on each Ollama model's
/// native function-calling format — this keeps behavior consistent across
/// every model in the Phase B fallback chain, including ones with weak or
/// no tool-calling support.
/// </summary>
public interface IAgentOrchestratorService
{
    Task<AgentResponseDto> HandleMessageAsync(int employeeId, SendMessageDto dto, CancellationToken ct = default);
}

public interface IChatSessionService
{
    Task<List<ChatSessionDto>> GetSessionsAsync(int employeeId, CancellationToken ct = default);
    Task<List<ChatMessageDto>> GetMessagesAsync(int sessionId, int employeeId, CancellationToken ct = default);
}