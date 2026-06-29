namespace EmployeeManagement.Domain.Entities;

public enum DocumentStatus
{
    Pending = 1,
    Processing = 2,
    Indexed = 3,
    Failed = 4
}

/// <summary>
/// An uploaded source document (contract, policy, SOP, etc.). The raw text
/// is split into DocumentChunk rows for retrieval — never queried directly
/// for RAG answers, only used for display/audit.
/// </summary>
public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;     // "text/plain", "application/pdf", etc.
    public string FullText { get; set; } = default!;          // extracted plain text, kept for re-chunking/audit

    public int UploadedByEmployeeId { get; set; }
    public Employee UploadedByEmployee { get; set; } = default!;

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int ChunkCount { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? IndexedAtUtc { get; set; }
}

/// <summary>
/// One retrievable chunk of a Document. Embedding is stored as a JSON-serialized
/// float[] in NVARCHAR(MAX) — chosen for portability across SQL Server editions.
/// Migration path: SQL Server 2025+ native VECTOR type can replace this column
/// with zero change to the chunking/retrieval logic above it (only EmbeddingService
/// storage/read calls would need updating).
/// </summary>
public class DocumentChunk
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public Document Document { get; set; } = default!;

    public int ChunkIndex { get; set; }
    public string Content { get; set; } = default!;
    public string EmbeddingJson { get; set; } = default!;     // JSON float[] from nomic-embed-text
    public int TokenCountEstimate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
