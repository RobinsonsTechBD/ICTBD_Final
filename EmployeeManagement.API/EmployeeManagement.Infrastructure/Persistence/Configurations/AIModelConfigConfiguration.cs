using EmployeeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeManagement.Infrastructure.Persistence.Configurations;

public class AIModelConfigConfiguration : IEntityTypeConfiguration<AIModelConfig>
{
    public void Configure(EntityTypeBuilder<AIModelConfig> b)
    {
        b.ToTable("AIModelConfigs");
        b.HasKey(x => x.Id);
        b.Property(x => x.ModelName).IsRequired().HasMaxLength(100);
        b.HasIndex(x => x.Priority);
    }
}
