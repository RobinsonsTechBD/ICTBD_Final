namespace EmployeeManagement.Domain.Entities;

public enum AttendanceStatus
{
    Present = 1,
    Late = 2,
    Absent = 3,
    OnLeave = 4,
    Holiday = 5,
    OffDay = 6,
    HalfDay = 7
}

public enum PunchType
{
    CheckIn = 1,
    CheckOut = 2
}

public enum LeaveType
{
    Casual = 1,
    Sick = 2,
    Earned = 3,
    Unpaid = 4,
    Maternity = 5,
    Paternity = 6
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum DataSource
{
    Device = 1,
    ManualByAdmin = 2,
    Simulated = 3
}
