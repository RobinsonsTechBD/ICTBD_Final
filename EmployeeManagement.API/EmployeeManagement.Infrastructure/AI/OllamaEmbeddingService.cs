using EmployeeManagement.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace EmployeeManagement.Infrastructure.AI;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public OllamaEmbeddingService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var baseUrl = _config["Ollama:LocalBaseUrl"] ?? "http://localhost:11434";
        var model = _config["Ollama:EmbeddingModel"] ?? "nomic-embed-text:latest";
        var client = _httpClientFactory.CreateClient("OllamaLocal");

        var body = new { model, prompt = text };
        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync($"{baseUrl.TrimEnd('/')}/api/embeddings", content, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Embedding model '{model}' timed out. Is it pulled? Run: ollama pull {model}");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach Ollama at {baseUrl} for embeddings. ({ex.Message})", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Ollama embeddings returned {(int)response.StatusCode}: {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("embedding", out var embeddingEl))
            throw new InvalidOperationException("Unexpected response shape from Ollama embeddings endpoint.");

        return embeddingEl.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }
}
