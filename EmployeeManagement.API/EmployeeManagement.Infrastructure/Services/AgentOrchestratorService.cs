using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.DTOs.Agent;
using EmployeeManagement.Application.DTOs.Attendance;
using EmployeeManagement.Application.DTOs.Rag;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ChatMessageDto = EmployeeManagement.Application.DTOs.AI.ChatMessageDto;

namespace EmployeeManagement.Infrastructure.Services;

public class AgentOrchestratorService : IAgentOrchestratorService
{
    private readonly ApplicationDbContext _db;
    private readonly IIntentClassifierService _intentClassifier;
    private readonly IModelFallbackChatService _chatService;
    private readonly IAttendanceService _attendanceService;
    private readonly ILeaveService _leaveService;
    private readonly IHolidayService _holidayService;
    private readonly IRagQueryService _ragQueryService;
    private readonly ILogger<AgentOrchestratorService> _logger;

    public AgentOrchestratorService(
        ApplicationDbContext db,
        IIntentClassifierService intentClassifier,
        IModelFallbackChatService chatService,
        IAttendanceService attendanceService,
        ILeaveService leaveService,
        IHolidayService holidayService,
        IRagQueryService ragQueryService,
        ILogger<AgentOrchestratorService> logger)
    {
        _db = db;
        _intentClassifier = intentClassifier;
        _chatService = chatService;
        _attendanceService = attendanceService;
        _leaveService = leaveService;
        _holidayService = holidayService;
        _ragQueryService = ragQueryService;
        _logger = logger;
    }

    public async Task<AgentResponseDto> HandleMessageAsync(int employeeId, SendMessageDto dto, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(employeeId, dto.SessionId, dto.Message, ct);

        _db.ChatMessages.Add(new ChatMessage { SessionId = session.Id, Role = "user", Content = dto.Message });
        await _db.SaveChangesAsync(ct);

        var classification = await _intentClassifier.ClassifyAsync(dto.Message, ct);
        _logger.LogInformation("Session {SessionId}: classified intent {Intent}", session.Id, classification.Intent);

        AgentResponseDto response = classification.Intent switch
        {
            AgentIntent.DocumentQuestion => await HandleDocumentQuestionAsync(session.Id, dto.Message, ct),
            AgentIntent.AttendanceSummary => await HandleAttendanceSummaryAsync(session.Id, employeeId, ct),
            AgentIntent.ApplyLeave => await HandleApplyLeaveAsync(session.Id, employeeId, dto.Message, ct),
            AgentIntent.CompanyHolidays => await HandleCompanyHolidaysAsync(session.Id, ct),
            _ => await HandleGeneralChatAsync(session.Id, dto.Message, ct)
        };

        response.SessionId = session.Id;

        _db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = response.Reply,
            DetectedIntent = response.Intent,
            ToolUsed = response.ToolUsed
        });
        session.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return response;
    }

    private async Task<ChatSession> GetOrCreateSessionAsync(int employeeId, int? sessionId, string firstMessage, CancellationToken ct)
    {
        if (sessionId.HasValue)
        {
            var existing = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.EmployeeId == employeeId, ct)
                ?? throw new KeyNotFoundException("Chat session not found, or does not belong to this employee.");
            return existing;
        }

        var session = new ChatSession
        {
            EmployeeId = employeeId,
            Title = firstMessage.Length > 60 ? firstMessage[..60] + "..." : firstMessage
        };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    // ---- Tool: Document Q&A (delegates to Phase C RAG pipeline) ----
    private async Task<AgentResponseDto> HandleDocumentQuestionAsync(int sessionId, string message, CancellationToken ct)
    {
        try
        {
            var ragResult = await _ragQueryService.AskAsync(new AskQuestionDto { Question = message, TopK = 5 }, ct);
            return new AgentResponseDto
            {
                Reply = ragResult.Answer,
                Intent = AgentIntent.DocumentQuestion,
                ToolUsed = "IRagQueryService.AskAsync",
                ModelUsed = ragResult.ModelUsed,
                Sources = ragResult.Sources.Cast<object>().ToList()
            };
        }
        catch (InvalidOperationException ex)
        {
            return new AgentResponseDto { Reply = $"I couldn't search the documents right now: {ex.Message}", Intent = AgentIntent.DocumentQuestion, ToolUsed = "IRagQueryService.AskAsync" };
        }
    }

    // ---- Tool: Attendance summary for the CURRENT calendar month ----
    private async Task<AgentResponseDto> HandleAttendanceSummaryAsync(int sessionId, int employeeId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var summaries = await _attendanceService.GetMonthlySummaryAsync(now.Year, now.Month, null, ct);
        var mine = summaries.FirstOrDefault(s => s.EmployeeId == employeeId);

        var reply = mine is null
            ? $"I don't have any attendance records for you yet for {now:MMMM yyyy}."
            : $"For {now:MMMM yyyy}: {mine.PresentDays} present, {mine.LateDays} late, {mine.AbsentDays} absent, " +
              $"{mine.LeaveDays} on leave, {mine.HolidayDays} holidays, {mine.OffDays} off-days, " +
              $"totaling {mine.TotalWorkedHours:0.#} worked hours.";

        return new AgentResponseDto { Reply = reply, Intent = AgentIntent.AttendanceSummary, ToolUsed = "IAttendanceService.GetMonthlySummaryAsync" };
    }

    // ---- Tool: Apply leave — extracts fields from free text via a structured LLM call ----
    private async Task<AgentResponseDto> HandleApplyLeaveAsync(int sessionId, int employeeId, string message, CancellationToken ct)
    {
        var extraction = await ExtractLeaveFieldsAsync(message, ct);

        if (!extraction.IsComplete)
        {
            // Known simplification (documented in README): this doesn't yet carry
            // partial slot-filling state across turns — the user needs to restate
            // the full request with the missing details in their next message.
            return new AgentResponseDto
            {
                Reply = extraction.MissingFieldsMessage ?? "I need a bit more detail — could you tell me the leave type, start date, end date, and reason?",
                Intent = AgentIntent.ApplyLeave,
                ToolUsed = "LeaveFieldExtraction (incomplete)"
            };
        }

        if (!Enum.TryParse<LeaveType>(extraction.LeaveType, true, out var leaveType))
            leaveType = LeaveType.Casual;

        try
        {
            var created = await _leaveService.CreateAsync(new CreateLeaveRequestDto
            {
                EmployeeId = employeeId,
                LeaveType = leaveType,
                StartDate = DateOnly.Parse(extraction.StartDate!),
                EndDate = DateOnly.Parse(extraction.EndDate!),
                Reason = extraction.Reason ?? "Requested via AI assistant"
            }, ct);

            return new AgentResponseDto
            {
                Reply = $"Done — I've submitted a {created.LeaveType} leave request for {created.StartDate:dd MMM} to {created.EndDate:dd MMM} ({created.TotalDays} day(s)). It's pending Manager/Admin approval.",
                Intent = AgentIntent.ApplyLeave,
                ToolUsed = "ILeaveService.CreateAsync"
            };
        }
        catch (InvalidOperationException ex)
        {
            return new AgentResponseDto { Reply = $"I couldn't submit that leave request: {ex.Message}", Intent = AgentIntent.ApplyLeave, ToolUsed = "ILeaveService.CreateAsync" };
        }
    }

    private async Task<LeaveExtractionResult> ExtractLeaveFieldsAsync(string message, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var systemPrompt = $@"
            Extract leave-application details from the user's message. Today's date is {today:yyyy-MM-dd}.
            Respond with ONLY a JSON object, no other text:
            {{""leaveType"": ""Casual|Sick|Earned|Unpaid|Maternity|Paternity or null"",
              ""startDate"": ""yyyy-MM-dd or null"",
              ""endDate"": ""yyyy-MM-dd or null"",
              ""reason"": ""short reason or null""}}
            Resolve relative dates (e.g. ""next Monday"", ""tomorrow"") into actual yyyy-MM-dd dates using today's date above.
            Use null for any field not mentioned or not confidently resolvable.
            ".Trim();

        var request = new ChatCompletionRequestDto
        {
            Temperature = 0.0,
            Messages = new List<ChatMessageDto>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = message }
            }
        };

        var completion = await _chatService.CompleteAsync(request, ct);
        var start = completion.Content.IndexOf('{');
        var end = completion.Content.LastIndexOf('}');

        if (start == -1 || end == -1 || end <= start)
            return new LeaveExtractionResult { IsComplete = false, MissingFieldsMessage = "I couldn't understand the leave details — please specify the leave type, start date, end date, and reason." };

        try
        {
            using var doc = JsonDocument.Parse(completion.Content[start..(end + 1)]);
            string? Get(string prop) => doc.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var result = new LeaveExtractionResult
            {
                LeaveType = Get("leaveType"),
                StartDate = Get("startDate"),
                EndDate = Get("endDate"),
                Reason = Get("reason")
            };

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(result.LeaveType)) missing.Add("leave type");
            if (string.IsNullOrWhiteSpace(result.StartDate)) missing.Add("start date");
            if (string.IsNullOrWhiteSpace(result.EndDate)) missing.Add("end date");

            result.IsComplete = missing.Count == 0;
            if (!result.IsComplete)
                result.MissingFieldsMessage = $"I need a bit more info to submit this leave request — could you confirm the {string.Join(", ", missing)}?";

            return result;
        }
        catch (JsonException)
        {
            return new LeaveExtractionResult { IsComplete = false, MissingFieldsMessage = "I couldn't understand the leave details — please specify the leave type, start date, end date, and reason." };
        }
    }

    // ---- Tool: Company holidays for the current year ----
    private async Task<AgentResponseDto> HandleCompanyHolidaysAsync(int sessionId, CancellationToken ct)
    {
        var holidays = await _holidayService.GetAllAsync(DateTime.UtcNow.Year, ct);
        var upcoming = holidays.Where(h => h.Date >= DateOnly.FromDateTime(DateTime.UtcNow)).OrderBy(h => h.Date).Take(5).ToList();

        var reply = upcoming.Count == 0
            ? "There are no upcoming holidays on record for this year."
            : "Upcoming holidays:\n" + string.Join("\n", upcoming.Select(h => $"- {h.Date:dd MMM yyyy}: {h.Name}"));

        return new AgentResponseDto { Reply = reply, Intent = AgentIntent.CompanyHolidays, ToolUsed = "IHolidayService.GetAllAsync" };
    }

    // ---- Fallback: plain conversational chat (also covers Unknown intent) ----
    private async Task<AgentResponseDto> HandleGeneralChatAsync(int sessionId, string message, CancellationToken ct)
    {
        //var history = await _db.ChatMessages.AsNoTracking()
        //    .Where(m => m.SessionId == sessionId)
        //    .OrderBy(m => m.CreatedAtUtc)
        //    .TakeLast(10)
        //    .ToListAsync(ct);
        var history = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);
            history.Reverse(); // back into chronological order — TakeLast() isn't translatable by EF Core's SQL provider

        var messages = new List<ChatMessageDto>
        {
            new() { Role = "system", Content = "You are a concise, helpful HR assistant for an employee management system. Keep replies brief and professional." }
        };
        messages.AddRange(history.Select(h => new ChatMessageDto { Role = h.Role, Content = h.Content }));

        var completion = await _chatService.CompleteAsync(new ChatCompletionRequestDto { Messages = messages, Temperature = 0.4 }, ct);

        return new AgentResponseDto { Reply = completion.Content, Intent = AgentIntent.GeneralChat, ToolUsed = null, ModelUsed = completion.ModelUsed };
    }
}