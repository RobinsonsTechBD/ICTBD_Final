using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.DTOs.Rag;

public class UploadDocumentDto
{
    public string Title { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "text/plain";
    /// <summary>Raw extracted text. For Phase C, callers extract text client-side
    /// or upload .txt/.md directly — PDF/DOCX binary extraction is a Phase C+ enhancement.</summary>
    public string Content { get; set; } = default!;
}

public class DocumentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public DocumentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int ChunkCount { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public DateTime? IndexedAtUtc { get; set; }
}

public class AskQuestionDto
{
    public string Question { get; set; } = default!;
    public int TopK { get; set; } = 5;
    public List<int>? DocumentIds { get; set; }   // null = search across all indexed documents
}

public class SourceReferenceDto
{
    public int DocumentId { get; set; }
    public string DocumentTitle { get; set; } = default!;
    public int ChunkIndex { get; set; }
    public string ExcerptPreview { get; set; } = default!;
    public double SimilarityScore { get; set; }
}

public class RagAnswerDto
{
    public string Answer { get; set; } = default!;
    public List<SourceReferenceDto> Sources { get; set; } = new();
    public string ModelUsed { get; set; } = default!;
    public bool AnsweredFromContext { get; set; }   // false if the model said it couldn't find an answer in the sources
}
