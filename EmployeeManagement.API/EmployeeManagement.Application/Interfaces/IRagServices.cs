using EmployeeManagement.Application.DTOs.Rag;

namespace EmployeeManagement.Application.Interfaces;

/// <summary>Wraps Ollama's /api/embeddings endpoint with the nomic-embed-text model.</summary>
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}

/// <summary>Splits long text into overlapping chunks suitable for embedding + retrieval.</summary>
public interface ITextChunker
{
    List<string> Chunk(string text, int maxChunkChars = 1200, int overlapChars = 150);
}

public interface IDocumentService
{
    Task<DocumentDto> UploadAndIndexAsync(UploadDocumentDto dto, int uploadedByEmployeeId, CancellationToken ct = default);
    Task<List<DocumentDto>> GetAllAsync(CancellationToken ct = default);
    Task<DocumentDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IRagQueryService
{
    Task<RagAnswerDto> AskAsync(AskQuestionDto dto, CancellationToken ct = default);
}
