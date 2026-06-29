using EmployeeManagement.Application.DTOs.Agent;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Services;

public class ChatSessionService : IChatSessionService
{
    private readonly ApplicationDbContext _db;
    public ChatSessionService(ApplicationDbContext db) => _db = db;

    public async Task<List<ChatSessionDto>> GetSessionsAsync(int employeeId, CancellationToken ct = default) =>
        await _db.ChatSessions.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Select(s => new ChatSessionDto { Id = s.Id, Title = s.Title, CreatedAtUtc = s.CreatedAtUtc, UpdatedAtUtc = s.UpdatedAtUtc })
            .ToListAsync(ct);

    public async Task<List<ChatMessageDto>> GetMessagesAsync(int sessionId, int employeeId, CancellationToken ct = default)
    {
        var belongsToEmployee = await _db.ChatSessions.AnyAsync(s => s.Id == sessionId && s.EmployeeId == employeeId, ct);
        if (!belongsToEmployee)
            throw new KeyNotFoundException("Chat session not found, or does not belong to this employee.");

        return await _db.ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                DetectedIntent = m.DetectedIntent,
                ToolUsed = m.ToolUsed,
                CreatedAtUtc = m.CreatedAtUtc
            })
            .ToListAsync(ct);
    }
}