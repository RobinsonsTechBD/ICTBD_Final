using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.DTOs.AI;

public class ChatMessageDto
{
    public string Role { get; set; } = default!;     // "system" | "user" | "assistant"
    public string Content { get; set; } = default!;
}

public class ChatCompletionRequestDto
{
    public List<ChatMessageDto> Messages { get; set; } = new();
    public double Temperature { get; set; } = 0.3;
    public bool RequireVision { get; set; } = false;   // if true, fallback chain only tries SupportsVision=true models
}

public class ChatCompletionResponseDto
{
    public string Content { get; set; } = default!;
    public string ModelUsed { get; set; } = default!;
    public ModelProviderType ProviderUsed { get; set; }
    public List<ModelAttemptDto> Attempts { get; set; } = new();
    public double ElapsedSeconds { get; set; }
}

/// <summary>One row per model tried, surfaced back to the caller for transparency/debugging.</summary>
public class ModelAttemptDto
{
    public string ModelName { get; set; } = default!;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double ElapsedSeconds { get; set; }
}

public class AIModelConfigDto
{
    public int Id { get; set; }
    public string ModelName { get; set; } = default!;
    public ModelProviderType Provider { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public string? BaseUrlOverride { get; set; }
    public int TimeoutSeconds { get; set; }
    public bool SupportsVision { get; set; }
    public bool? LastHealthOk { get; set; }
}

public class ReorderModelDto
{
    public int Id { get; set; }
    public int NewPriority { get; set; }
}
