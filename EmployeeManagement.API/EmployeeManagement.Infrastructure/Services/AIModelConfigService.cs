using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Application.Services;

public class AIModelConfigService : IAIModelConfigService
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IChatProvider> _providers;

    public AIModelConfigService(ApplicationDbContext db, IEnumerable<IChatProvider> providers)
    {
        _db = db;
        _providers = providers;
    }

    public async Task<List<AIModelConfigDto>> GetChainAsync(CancellationToken ct = default) =>
        await _db.AIModelConfigs.AsNoTracking().OrderBy(m => m.Priority).Select(m => MapToDto(m)).ToListAsync(ct);

    public async Task<AIModelConfigDto> CreateAsync(AIModelConfigDto dto, CancellationToken ct = default)
    {
        var entity = new AIModelConfig
        {
            ModelName = dto.ModelName,
            Provider = dto.Provider,
            Priority = dto.Priority,
            IsEnabled = dto.IsEnabled,
            BaseUrlOverride = dto.BaseUrlOverride,
            TimeoutSeconds = dto.TimeoutSeconds <= 0 ? 30 : dto.TimeoutSeconds,
            SupportsVision = dto.SupportsVision
        };
        _db.AIModelConfigs.Add(entity);
        await _db.SaveChangesAsync(ct);
        dto.Id = entity.Id;
        return dto;
    }

    public async Task UpdateAsync(int id, AIModelConfigDto dto, CancellationToken ct = default)
    {
        var entity = await _db.AIModelConfigs.FindAsync(new object[] { id }, ct)
            ?? throw new KeyNotFoundException("Model config not found.");

        entity.ModelName = dto.ModelName;
        entity.Provider = dto.Provider;
        entity.Priority = dto.Priority;
        entity.IsEnabled = dto.IsEnabled;
        entity.BaseUrlOverride = dto.BaseUrlOverride;
        entity.TimeoutSeconds = dto.TimeoutSeconds;
        entity.SupportsVision = dto.SupportsVision;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Bulk re-prioritize — e.g. drag-and-drop reordering in an admin UI.</summary>
    public async Task ReorderAsync(List<ReorderModelDto> newOrder, CancellationToken ct = default)
    {
        var ids = newOrder.Select(o => o.Id).ToList();
        var entities = await _db.AIModelConfigs.Where(m => ids.Contains(m.Id)).ToListAsync(ct);

        foreach (var item in newOrder)
        {
            var entity = entities.FirstOrDefault(e => e.Id == item.Id);
            if (entity is not null) entity.Priority = item.NewPriority;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Pings each enabled model with a trivial 1-token prompt to verify it's
    /// actually reachable/pulled, without waiting for a real chat workload.
    /// </summary>
    public async Task<List<AIModelConfigDto>> RunHealthCheckAsync(CancellationToken ct = default)
    {
        var models = await _db.AIModelConfigs.Where(m => m.IsEnabled).ToListAsync(ct);
        var probeMessage = new List<ChatMessageDto> { new() { Role = "user", Content = "ping" } };

        foreach (var model in models)
        {
            var provider = _providers.FirstOrDefault(p => p.Provider == model.Provider);
            try
            {
                if (provider is null) throw new InvalidOperationException("No provider registered.");
                await provider.CompleteAsync(model, probeMessage, 0.1, ct);
                model.LastHealthOk = true;
            }
            catch
            {
                model.LastHealthOk = false;
            }
            model.LastHealthCheckUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return models.OrderBy(m => m.Priority).Select(m => MapToDto(m)).ToList();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.AIModelConfigs.FindAsync(new object[] { id }, ct);
        if (entity is null) return;
        _db.AIModelConfigs.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static AIModelConfigDto MapToDto(AIModelConfig m) => new()
    {
        Id = m.Id,
        ModelName = m.ModelName,
        Provider = m.Provider,
        Priority = m.Priority,
        IsEnabled = m.IsEnabled,
        BaseUrlOverride = m.BaseUrlOverride,
        TimeoutSeconds = m.TimeoutSeconds,
        SupportsVision = m.SupportsVision,
        LastHealthOk = m.LastHealthOk
    };
}
