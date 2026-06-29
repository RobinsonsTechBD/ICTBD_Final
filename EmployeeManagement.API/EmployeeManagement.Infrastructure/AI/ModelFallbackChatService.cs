using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EmployeeManagement.Infrastructure.AI;

public class ModelFallbackChatService : IModelFallbackChatService
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IChatProvider> _providers;
    private readonly ILogger<ModelFallbackChatService> _logger;

    public ModelFallbackChatService(ApplicationDbContext db, IEnumerable<IChatProvider> providers, ILogger<ModelFallbackChatService> logger)
    {
        _db = db;
        _providers = providers;
        _logger = logger;
    }

    public async Task<ChatCompletionResponseDto> CompleteAsync(ChatCompletionRequestDto request, CancellationToken ct = default)
    {
        if (request.Messages.Count == 0)
            throw new ArgumentException("At least one message is required.");

        var chainQuery = _db.AIModelConfigs.AsNoTracking().Where(m => m.IsEnabled);
        if (request.RequireVision)
            chainQuery = chainQuery.Where(m => m.SupportsVision);

        var chain = await chainQuery.OrderBy(m => m.Priority).ToListAsync(ct);
        if (chain.Count == 0)
            throw new InvalidOperationException(request.RequireVision
                ? "No enabled vision-capable models configured in AIModelConfig."
                : "No enabled models configured in AIModelConfig. Seed at least one row.");

        var attempts = new List<ModelAttemptDto>();
        var overallStopwatch = Stopwatch.StartNew();

        foreach (var model in chain)
        {
            var provider = _providers.FirstOrDefault(p => p.Provider == model.Provider);
            if (provider is null)
            {
                attempts.Add(new ModelAttemptDto { ModelName = model.ModelName, Success = false, ErrorMessage = $"No IChatProvider registered for {model.Provider}." });
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("Attempting chat completion with model {ModelName} ({Provider})", model.ModelName, model.Provider);
                var result = await provider.CompleteAsync(model, request.Messages, request.Temperature, ct);
                sw.Stop();

                attempts.Add(new ModelAttemptDto { ModelName = model.ModelName, Success = true, ElapsedSeconds = sw.Elapsed.TotalSeconds });

                return new ChatCompletionResponseDto
                {
                    Content = result,
                    ModelUsed = model.ModelName,
                    ProviderUsed = model.Provider,
                    Attempts = attempts,
                    ElapsedSeconds = overallStopwatch.Elapsed.TotalSeconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogWarning(ex, "Model {ModelName} failed, falling back to next in chain.", model.ModelName);
                attempts.Add(new ModelAttemptDto { ModelName = model.ModelName, Success = false, ErrorMessage = ex.Message, ElapsedSeconds = sw.Elapsed.TotalSeconds });
                // continue to next model in the chain
            }
        }

        var triedNames = string.Join(", ", attempts.Select(a => a.ModelName));
        throw new InvalidOperationException($"All models in the fallback chain failed: {triedNames}. See Attempts log for details.")
        {
            Data = { ["Attempts"] = attempts }
        };
    }
}
