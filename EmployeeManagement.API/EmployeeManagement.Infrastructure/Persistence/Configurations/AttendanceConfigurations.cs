using EmployeeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeManagement.Infrastructure.Persistence.Configurations;

public class WorkShiftConfiguration : IEntityTypeConfiguration<WorkShift>
{
    public void Configure(EntityTypeBuilder<WorkShift> b)
    {
        b.ToTable("WorkShifts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("Holidays");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(150);
        b.HasIndex(x => x.Date);
    }
}

public class OffDayScheduleConfiguration : IEntityTypeConfiguration<OffDaySchedule>
{
    public void Configure(EntityTypeBuilder<OffDaySchedule> b)
    {
        b.ToTable("OffDaySchedules");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.ToTable("LeaveRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovedByEmployee).WithMany().HasForeignKey(x => x.ApprovedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.StartDate, x.EndDate });
    }
}

public class AttendanceDeviceConfiguration : IEntityTypeConfiguration<AttendanceDevice>
{
    public void Configure(EntityTypeBuilder<AttendanceDevice> b)
    {
        b.ToTable("AttendanceDevices");
        b.HasKey(x => x.Id);
        b.Property(x => x.DeviceCode).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.DeviceCode).IsUnique();
    }
}

public class AttendanceDeviceLogConfiguration : IEntityTypeConfiguration<AttendanceDeviceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceDeviceLog> b)
    {
        b.ToTable("AttendanceDeviceLogs");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.EmployeeId, x.PunchTimeUtc });
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> b)
    {
        b.ToTable("Attendances");
        b.HasKey(x => x.Id);
        b.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.LeaveRequest).WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.SetNull);
        // One attendance row per employee per day — critical for idempotent processing
        b.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
    }
}
