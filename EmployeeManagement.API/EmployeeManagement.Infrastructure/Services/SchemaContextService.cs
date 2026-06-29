using EmployeeManagement.Application.Interfaces;

namespace EmployeeManagement.Infrastructure.Services;

/// <summary>
/// Returns a curated, role-filtered schema description injected into the
/// SQL-generation system prompt. Critically, this is NOT auto-generated from
/// INFORMATION_SCHEMA — it's hand-curated to:
/// 1. Exclude tables a role must never query (e.g. AspNetUsers/passwords for non-Admin)
/// 2. Include only the columns relevant to HR queries (skip EmbeddingJson, FullText, etc.)
/// 3. Include example values in comments so the LLM understands enums
/// 4. Describe join paths explicitly so the LLM generates correct JOINs
/// </summary>
public class SchemaContextService : ISchemaContextService
{
    // Schema visible to ALL authenticated roles
    private const string BaseSchema = """
        -- DATABASE: EmployeeManagementDb (MS SQL Server 2022)
        -- IMPORTANT: Generate only SELECT queries. Never use INSERT, UPDATE, DELETE, DROP, EXEC, or sp_.

        -- Departments
        CREATE TABLE Departments (
            Id INT PRIMARY KEY,
            Name NVARCHAR(100),    -- e.g. 'Marketing', 'Sales', 'Purchase', 'Delivery'
            IsActive BIT
        );

        -- Employees
        CREATE TABLE Employees (
            Id INT PRIMARY KEY,
            EmployeeCode NVARCHAR(20),   -- e.g. 'EMP-001'
            FullName NVARCHAR(150),
            Email NVARCHAR(150),
            Role NVARCHAR(50),           -- 'Admin','Manager','Marketing','Sales','Purchase','Delivery'
            DepartmentId INT REFERENCES Departments(Id),
            IsActive BIT,
            CreatedAtUtc DATETIME2
        );

        -- WorkShifts
        CREATE TABLE WorkShifts (
            Id INT PRIMARY KEY,
            Name NVARCHAR(100),          -- e.g. 'General Shift'
            StartTime TIME,              -- 10:00:00
            EndTime TIME,                -- 19:00:00
            GraceMinutes INT,
            IsActive BIT
        );

        -- Attendances (one row per employee per day — this is the main reporting table)
        CREATE TABLE Attendances (
            Id INT PRIMARY KEY,
            EmployeeId INT REFERENCES Employees(Id),
            Date DATE,
            CheckInTime TIME,
            CheckOutTime TIME,
            WorkedHours FLOAT,
            Status INT,      -- 1=Present, 2=Late, 3=Absent, 4=OnLeave, 5=Holiday, 6=OffDay, 7=HalfDay
            LateByMinutes INT,
            Remarks NVARCHAR(MAX),
            Source INT       -- 1=Device, 2=ManualByAdmin, 3=Simulated
        );

        -- Holidays
        CREATE TABLE Holidays (
            Id INT PRIMARY KEY,
            Name NVARCHAR(150),
            Date DATE,
            IsRecurringYearly BIT,
            Description NVARCHAR(MAX)
        );

        -- LeaveRequests
        CREATE TABLE LeaveRequests (
            Id INT PRIMARY KEY,
            EmployeeId INT REFERENCES Employees(Id),
            LeaveType INT,   -- 1=Casual, 2=Sick, 3=Earned, 4=Unpaid, 5=Maternity, 6=Paternity
            StartDate DATE,
            EndDate DATE,
            Reason NVARCHAR(500),
            Status INT,      -- 1=Pending, 2=Approved, 3=Rejected, 4=Cancelled
            ApprovedByEmployeeId INT REFERENCES Employees(Id),
            ActionedAtUtc DATETIME2,
            CreatedAtUtc DATETIME2
        );

        -- Documents (RAG source documents)
        CREATE TABLE Documents (
            Id INT PRIMARY KEY,
            Title NVARCHAR(250),
            FileName NVARCHAR(250),
            Status INT,      -- 1=Pending, 2=Processing, 3=Indexed, 4=Failed
            ChunkCount INT,
            UploadedAtUtc DATETIME2,
            IndexedAtUtc DATETIME2
        );

        -- ChatSessions (AI agent conversations)
        CREATE TABLE ChatSessions (
            Id INT PRIMARY KEY,
            EmployeeId INT REFERENCES Employees(Id),
            Title NVARCHAR(150),
            CreatedAtUtc DATETIME2,
            UpdatedAtUtc DATETIME2
        );
        """;

    // Additional tables visible only to Admin and Manager roles
    private const string AdminManagerSchema = """

        -- AIModelConfigs (fallback chain configuration)
        CREATE TABLE AIModelConfigs (
            Id INT PRIMARY KEY,
            ModelName NVARCHAR(100),
            Provider INT,        -- 1=Local, 2=Cloud
            Priority INT,
            IsEnabled BIT,
            TimeoutSeconds INT,
            SupportsVision BIT,
            LastHealthOk BIT,
            LastHealthCheckUtc DATETIME2
        );
        """;

    public string GetSchemaForRole(string role)
    {
        var schema = BaseSchema;
        if (role is "Admin" or "Manager")
            schema += AdminManagerSchema;
        return schema;
    }
}