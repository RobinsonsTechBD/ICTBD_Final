using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EmployeeManagement.Infrastructure.AI;

/// <summary>
/// Talks to any OpenAI-compatible "/v1/chat/completions" endpoint. This covers
/// Ollama's own cloud offering as well as drop-in alternatives (OpenRouter,
/// Groq, Together, etc.) without code changes — only Ollama:CloudBaseUrl and
/// Ollama:CloudApiKey in appsettings need to change.
/// </summary>
public class OllamaCloudProvider : IChatProvider
{
    public ModelProviderType Provider => ModelProviderType.Cloud;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public OllamaCloudProvider(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<string> CompleteAsync(AIModelConfig model, List<ChatMessageDto> messages, double temperature, CancellationToken ct)
    {
        var baseUrl = model.BaseUrlOverride ?? _config["Ollama:CloudBaseUrl"]
            ?? throw new InvalidOperationException("Ollama:CloudBaseUrl is not configured.");
        var apiKey = _config["Ollama:CloudApiKey"];

        var client = _httpClientFactory.CreateClient("OllamaCloud");
        if (!string.IsNullOrWhiteSpace(apiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var body = new
        {
            model = model.ModelName,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature,
            stream = false
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(model.TimeoutSeconds));

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync($"{baseUrl.TrimEnd('/')}/v1/chat/completions", content, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Cloud model '{model.ModelName}' timed out after {model.TimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach cloud endpoint {baseUrl}. ({ex.Message})", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Cloud provider returned {(int)response.StatusCode} for model '{model.ModelName}': {errorBody}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        var contentText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return contentText ?? string.Empty;
    }
}
