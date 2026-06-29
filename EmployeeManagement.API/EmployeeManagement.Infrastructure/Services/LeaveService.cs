using EmployeeManagement.Application.DTOs.Attendance;
using EmployeeManagement.Application.DTOs.Common;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Services;

public class LeaveService : ILeaveService
{
    private readonly ApplicationDbContext _db;
    public LeaveService(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<LeaveRequestDto>> GetLeaveRequestsAsync(LeaveQueryParams query, CancellationToken ct = default)
    {
        var q = _db.LeaveRequests.Include(l => l.Employee).Include(l => l.ApprovedByEmployee).AsNoTracking().AsQueryable();

        if (query.EmployeeId.HasValue) q = q.Where(l => l.EmployeeId == query.EmployeeId);
        if (query.Status.HasValue) q = q.Where(l => l.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(l => l.Employee.FullName.Contains(query.Search) || l.Reason.Contains(query.Search));

        q = query.SortDescending ? q.OrderByDescending(l => l.CreatedAtUtc) : q.OrderBy(l => l.CreatedAtUtc);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(l => new LeaveRequestDto
            {
                Id = l.Id,
                EmployeeId = l.EmployeeId,
                EmployeeName = l.Employee.FullName,
                LeaveType = l.LeaveType,
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                TotalDays = l.EndDate.DayNumber - l.StartDate.DayNumber + 1,
                Reason = l.Reason,
                Status = l.Status,
                ApprovedByName = l.ApprovedByEmployee != null ? l.ApprovedByEmployee.FullName : null,
                ActionRemarks = l.ActionRemarks
            }).ToListAsync(ct);

        return new PagedResult<LeaveRequestDto> { Items = items, TotalCount = total, PageNumber = query.PageNumber, PageSize = query.PageSize };
    }

    public async Task<LeaveRequestDto> CreateAsync(CreateLeaveRequestDto dto, CancellationToken ct = default)
    {
        if (dto.EndDate < dto.StartDate)
            throw new InvalidOperationException("EndDate cannot be before StartDate.");

        var overlapping = await _db.LeaveRequests.AnyAsync(l =>
            l.EmployeeId == dto.EmployeeId &&
            l.Status != LeaveStatus.Rejected && l.Status != LeaveStatus.Cancelled &&
            l.StartDate <= dto.EndDate && l.EndDate >= dto.StartDate, ct);
        if (overlapping)
            throw new InvalidOperationException("An overlapping leave request already exists for this period.");

        var entity = new LeaveRequest
        {
            EmployeeId = dto.EmployeeId,
            LeaveType = dto.LeaveType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason,
            Status = LeaveStatus.Pending
        };
        _db.LeaveRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetLeaveRequestsAsync(new LeaveQueryParams { EmployeeId = dto.EmployeeId, PageSize = 1, SortDescending = true }, ct)).Items.First();
    }

    public async Task<LeaveRequestDto> ActionAsync(int leaveRequestId, int actionedByEmployeeId, LeaveActionDto dto, CancellationToken ct = default)
    {
        var entity = await _db.LeaveRequests.FirstOrDefaultAsync(l => l.Id == leaveRequestId, ct)
            ?? throw new KeyNotFoundException("Leave request not found.");

        if (entity.Status != LeaveStatus.Pending)
            throw new InvalidOperationException("Only pending leave requests can be actioned.");

        entity.Status = dto.Status;
        entity.ApprovedByEmployeeId = actionedByEmployeeId;
        entity.ActionedAtUtc = DateTime.UtcNow;
        entity.ActionRemarks = dto.ActionRemarks;

        await _db.SaveChangesAsync(ct);

        return (await GetLeaveRequestsAsync(new LeaveQueryParams { EmployeeId = entity.EmployeeId, PageSize = 1, SortDescending = true }, ct)).Items.First();
    }
}
