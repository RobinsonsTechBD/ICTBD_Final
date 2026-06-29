using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.DTOs.Vision;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace EmployeeManagement.Infrastructure.Services;

public class VisionAnalysisService : IVisionAnalysisService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<VisionAnalysisService> _logger;

    public VisionAnalysisService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<VisionAnalysisService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<VisionAnalysisResponseDto> AnalyseAsync(VisionAnalysisRequestDto request, CancellationToken ct = default)
    {
        // Find the highest-priority enabled vision-capable model
        var model = await _db.AIModelConfigs.AsNoTracking()
            .Where(m => m.IsEnabled && m.SupportsVision)
            .OrderBy(m => m.Priority)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "No enabled vision-capable model found in AIModelConfig. " +
                "Add a row with SupportsVision=true (e.g. modelName='llava:latest', provider=Local) via PUT /api/aimodelconfig.");

        var prompt = BuildPrompt(request);
        var sw = Stopwatch.StartNew();

        var response = await CallOllamaVisionAsync(model, request.ImageBase64, request.MimeType, prompt, ct);
        sw.Stop();

        return new VisionAnalysisResponseDto
        {
            Analysis = response,
            TaskType = request.TaskType,
            ModelUsed = model.ModelName,
            ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2),
            ExtractedText = request.TaskType is VisionTaskType.DocumentOCR or VisionTaskType.AttendanceDevicePhoto
                ? response
                : null
        };
    }

    /// <summary>
    /// Calls Ollama's /api/generate endpoint with the images array — this is the
    /// multimodal path; /api/chat also supports images but /api/generate is more
    /// universally supported across LLaVA versions.
    /// </summary>
    private async Task<string> CallOllamaVisionAsync(
        AIModelConfig model, string imageBase64, string mimeType, string prompt, CancellationToken ct)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(model.BaseUrlOverride)
            ? model.BaseUrlOverride
            : (_config["Ollama:LocalBaseUrl"] ?? "http://localhost:11434");

        var client = _httpClientFactory.CreateClient("OllamaLocal");

        var body = new
        {
            model = model.ModelName,
            //model = "llava:latest",
            prompt,
            images = new[] { imageBase64 },  // Ollama expects raw base64, no data: URI prefix
            stream = false
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(model.TimeoutSeconds));

        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await client.PostAsync($"{baseUrl.TrimEnd('/')}/api/generate", content, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Vision model '{model.ModelName}' timed out after {model.TimeoutSeconds}s. Large images on CPU can be slow — consider increasing TimeoutSeconds for this model in AIModelConfig.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not reach Ollama at {baseUrl}. ({ex.Message})", ex);
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var error = await httpResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Ollama vision returned {(int)httpResponse.StatusCode}: {error}");
        }

        var json = await httpResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }

    private static string BuildPrompt(VisionAnalysisRequestDto request)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomPrompt))
            return request.CustomPrompt;

        return request.TaskType switch
        {
            VisionTaskType.DocumentOCR =>
                "You are an OCR engine. Extract ALL text visible in this image exactly as it appears, preserving layout where possible. Output only the extracted text, nothing else.",

            VisionTaskType.AttendanceDevicePhoto =>
                "This is a photo of a biometric attendance device screen. Extract: " +
                "1) Any employee ID or name shown, 2) The time displayed, 3) Whether it shows check-in or check-out, " +
                "4) Any status message (Access Granted/Denied etc.). Format as: ID: ... | Time: ... | Type: ... | Status: ...",

            VisionTaskType.FaceDetectionCheck =>
                "Is there a human face visible in this image? Answer with only YES or NO, followed by a brief reason (e.g. YES - one face clearly visible facing camera).",

            _ =>
                "Describe this image in detail, including any visible text, people, objects, and the overall context."
        };
    }
}