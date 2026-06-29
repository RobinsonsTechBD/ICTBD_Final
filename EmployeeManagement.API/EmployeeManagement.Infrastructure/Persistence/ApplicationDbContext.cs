using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagement.Infrastructure.Persistence;

/// <summary>
/// REPLACES the ApplicationDbContext.cs from the Phase A package.
/// Now inherits IdentityDbContext so AspNetUsers/AspNetRoles/etc. are created
/// alongside your business tables in the same database via the same migrations.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // ---- Core entities ----
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();

    // ---- Attendance module ----
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<OffDaySchedule> OffDaySchedules => Set<OffDaySchedule>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<AttendanceDevice> AttendanceDevices => Set<AttendanceDevice>();
    public DbSet<AttendanceDeviceLog> AttendanceDeviceLogs => Set<AttendanceDeviceLog>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<AIModelConfig> AIModelConfigs => Set<AIModelConfig>();

    // ---- RAG module ----
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    // ---- Agent Intent Orchastrator --------
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IMPORTANT: must be called first — sets up AspNetUsers, AspNetRoles, etc.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
