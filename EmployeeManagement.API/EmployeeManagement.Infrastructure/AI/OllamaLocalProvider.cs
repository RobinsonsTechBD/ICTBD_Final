using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace EmployeeManagement.Infrastructure.AI;

public class OllamaLocalProvider : IChatProvider
{
    public ModelProviderType Provider => ModelProviderType.Local;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public OllamaLocalProvider(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<string> CompleteAsync(AIModelConfig model, List<ChatMessageDto> messages, double temperature, CancellationToken ct)
    {
        var baseUrl = model.BaseUrlOverride ?? _config["Ollama:LocalBaseUrl"] ?? "http://localhost:11434";
        var client = _httpClientFactory.CreateClient("OllamaLocal");

        var body = new
        {
            model = model.ModelName,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false,
            options = new { temperature }
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(model.TimeoutSeconds));

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync($"{baseUrl.TrimEnd('/')}/api/chat", content, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Ollama local model '{model.ModelName}' timed out after {model.TimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach local Ollama at {baseUrl}. Is 'ollama serve' running? ({ex.Message})", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Ollama returned {(int)response.StatusCode} for model '{model.ModelName}': {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("message", out var messageEl) ||
            !messageEl.TryGetProperty("content", out var contentEl))
        {
            throw new InvalidOperationException($"Unexpected response shape from Ollama for model '{model.ModelName}'.");
        }

        return contentEl.GetString() ?? string.Empty;
    }
}
