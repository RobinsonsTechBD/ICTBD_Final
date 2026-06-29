using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.Interfaces;

/// <summary>
/// One implementation per transport: Local Ollama HTTP API, and a generic
/// OpenAI-compatible Cloud HTTP API. The fallback engine picks which to call
/// based on AIModelConfig.Provider — it never talks HTTP directly.
/// </summary>
public interface IChatProvider
{
    ModelProviderType Provider { get; }
    Task<string> CompleteAsync(AIModelConfig model, List<ChatMessageDto> messages, double temperature, CancellationToken ct);
}

/// <summary>
/// The fallback chain engine. Tries each enabled AIModelConfig in Priority
/// order; on failure (timeout, connection refused, non-2xx, model not pulled)
/// moves to the next. Throws only if every model in the chain fails.
/// </summary>
public interface IModelFallbackChatService
{
    Task<ChatCompletionResponseDto> CompleteAsync(ChatCompletionRequestDto request, CancellationToken ct = default);
}

public interface IAIModelConfigService
{
    Task<List<AIModelConfigDto>> GetChainAsync(CancellationToken ct = default);
    Task<AIModelConfigDto> CreateAsync(AIModelConfigDto dto, CancellationToken ct = default);
    Task UpdateAsync(int id, AIModelConfigDto dto, CancellationToken ct = default);
    Task ReorderAsync(List<ReorderModelDto> newOrder, CancellationToken ct = default);
    Task<List<AIModelConfigDto>> RunHealthCheckAsync(CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}