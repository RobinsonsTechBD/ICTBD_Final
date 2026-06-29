namespace EmployeeManagement.Domain.Entities;

public enum ModelProviderType
{
    Local = 1,   // Ollama running on localhost (or LAN) — e.g. http://localhost:11434
    Cloud = 2    // Any OpenAI-compatible cloud endpoint (Ollama Cloud, OpenRouter, Groq, etc.)
}

/// <summary>
/// One row per model in the fallback chain. Priority determines try-order
/// (lowest number tried first). Editable at runtime via AIModelConfigController
/// — no redeploy needed to reorder, disable, or swap models.
/// Seeded default chain: Qwen3.5 (Local) -> Llama3.1 (Local) -> Qwen3.5VL (Local) -> Gemma4 (Cloud).
/// </summary>
public class AIModelConfig
{
    public int Id { get; set; }
    public string ModelName { get; set; } = default!;     // must match the exact Ollama tag, e.g. "qwen2.5:7b"
    public ModelProviderType Provider { get; set; }
    public int Priority { get; set; }                      // 1 = tried first
    public bool IsEnabled { get; set; } = true;

    public string? BaseUrlOverride { get; set; }            // null = use Ollama:LocalBaseUrl / Ollama:CloudBaseUrl from config
    public int TimeoutSeconds { get; set; } = 30;

    public bool SupportsVision { get; set; }                 // true for LLaVA / VL models (Phase F will use this)

    public DateTime? LastHealthCheckUtc { get; set; }
    public bool? LastHealthOk { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}