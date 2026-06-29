namespace EmployeeManagement.Domain.Entities;

/// <summary>
/// Represents a physical attendance device (biometric/RFID). In this project,
/// real hardware is replaced by a simulator service that writes rows here
/// exactly as a real device integration would (via a webhook/polling endpoint).
/// </summary>
public class AttendanceDevice
{
    public int Id { get; set; }
    public string DeviceCode { get; set; } = default!;   // "MAIN-GATE-01"
    public string Location { get; set; } = default!;     // "Head Office - Dhaka"
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Raw, immutable punch events as received from a device (or the simulator).
/// The AttendanceService processes these nightly/on-demand into the
/// aggregated Attendance record (one row per employee per day).
/// </summary>
public class AttendanceDeviceLog
{
    public long Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;

    public int DeviceId { get; set; }
    public AttendanceDevice Device { get; set; } = default!;

    public PunchType PunchType { get; set; }
    public DateTime PunchTimeUtc { get; set; }
    public DataSource Source { get; set; } = DataSource.Simulated;
}
