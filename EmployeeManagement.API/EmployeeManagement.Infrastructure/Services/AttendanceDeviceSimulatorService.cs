using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace EmployeeManagement.Infrastructure.Services;

/// <summary>
/// Stands in for a real biometric/RFID device integration. In production this
/// would be replaced by a webhook receiver or polling client for the actual
/// device SDK — the rest of the pipeline (AttendanceService.ProcessAttendanceForDateAsync)
/// is identical either way, since both write to AttendanceDeviceLog.
/// </summary>
public class AttendanceDeviceSimulatorService : IAttendanceDeviceSimulatorService
{
    private readonly ApplicationDbContext _db;
    private static readonly Random _rng = new();

    public AttendanceDeviceSimulatorService(ApplicationDbContext db) => _db = db;

    public async Task<int> GenerateDemoPunchesAsync(DateOnly date, CancellationToken ct = default)
    {
        var device = await _db.AttendanceDevices.FirstOrDefaultAsync(d => d.IsActive, ct);
        if (device is null)
        {
            device = new AttendanceDevice { DeviceCode = "MAIN-GATE-01", Location = "Head Office - Dhaka", IsActive = true };
            _db.AttendanceDevices.Add(device);
            await _db.SaveChangesAsync(ct);
        }

        var shift = await _db.WorkShifts.FirstOrDefaultAsync(s => s.IsActive, ct)
            ?? throw new InvalidOperationException("Seed an active WorkShift before simulating punches.");

        var employees = await _db.Employees.Where(e => e.IsActive).ToListAsync(ct);
        int created = 0;

        foreach (var emp in employees)
        {
            // 8% chance of being absent (no punches at all) to produce realistic demo data
            if (_rng.NextDouble() < 0.08) continue;

            // Spread check-in between 30 min early and 45 min late around shift start
            var offsetMinutes = _rng.Next(-30, 46);
            var checkInDateTime = date.ToDateTime(TimeOnly.FromTimeSpan(shift.StartTime)).AddMinutes(offsetMinutes);

            _db.AttendanceDeviceLogs.Add(new AttendanceDeviceLog
            {
                EmployeeId = emp.Id,
                DeviceId = device.Id,
                PunchType = PunchType.CheckIn,
                PunchTimeUtc = checkInDateTime,
                Source = DataSource.Simulated
            });
            created++;

            // 5% chance of missing check-out (still counts as worked day, just no WorkedHours)
            if (_rng.NextDouble() > 0.05)
            {
                var checkOutDateTime = date.ToDateTime(TimeOnly.FromTimeSpan(shift.EndTime)).AddMinutes(_rng.Next(-15, 60));
                _db.AttendanceDeviceLogs.Add(new AttendanceDeviceLog
                {
                    EmployeeId = emp.Id,
                    DeviceId = device.Id,
                    PunchType = PunchType.CheckOut,
                    PunchTimeUtc = checkOutDateTime,
                    Source = DataSource.Simulated
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return created;
    }
}
