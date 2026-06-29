using EmployeeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeManagement.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("Departments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("Employees");
        b.HasKey(x => x.Id);
        b.Property(x => x.EmployeeCode).IsRequired().HasMaxLength(20);
        b.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        b.Property(x => x.Email).IsRequired().HasMaxLength(150);
        b.Property(x => x.Role).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.EmployeeCode).IsUnique();
        b.HasIndex(x => x.Email).IsUnique();

        b.HasOne(x => x.Department).WithMany(d => d.Employees)
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.WorkShift).WithMany(s => s.Employees)
            .HasForeignKey(x => x.WorkShiftId).OnDelete(DeleteBehavior.SetNull);
    }
}