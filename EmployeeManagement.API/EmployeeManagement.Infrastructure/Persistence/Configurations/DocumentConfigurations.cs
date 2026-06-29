using EmployeeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployeeManagement.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("Documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(250);
        b.Property(x => x.FileName).IsRequired().HasMaxLength(250);
        b.Property(x => x.FullText).IsRequired();   // NVARCHAR(MAX) by default for unbounded string
        b.HasOne(x => x.UploadedByEmployee).WithMany().HasForeignKey(x => x.UploadedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("DocumentChunks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.EmbeddingJson).IsRequired();   // NVARCHAR(MAX) JSON float[]
        b.HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.DocumentId, x.ChunkIndex });
    }
}
