using EmployeeManagement.Application.DTOs.Rag;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EmployeeManagement.Application.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _db;
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(ApplicationDbContext db, ITextChunker chunker, IEmbeddingService embeddingService, ILogger<DocumentService> logger)
    {
        _db = db;
        _chunker = chunker;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<DocumentDto> UploadAndIndexAsync(UploadDocumentDto dto, int uploadedByEmployeeId, CancellationToken ct = default)
    {
        var document = new Document
        {
            Title = dto.Title,
            FileName = dto.FileName,
            ContentType = dto.ContentType,
            FullText = dto.Content,
            UploadedByEmployeeId = uploadedByEmployeeId,
            Status = DocumentStatus.Processing
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        try
        {
            var chunks = _chunker.Chunk(dto.Content);
            if (chunks.Count == 0)
                throw new InvalidOperationException("No extractable text content — document is empty after trimming.");

            int index = 0;
            foreach (var chunkText in chunks)
            {
                var embedding = await _embeddingService.EmbedAsync(chunkText, ct);

                _db.DocumentChunks.Add(new DocumentChunk
                {
                    DocumentId = document.Id,
                    ChunkIndex = index++,
                    Content = chunkText,
                    EmbeddingJson = JsonSerializer.Serialize(embedding),
                    TokenCountEstimate = chunkText.Length / 4   // rough heuristic, ~4 chars/token
                });
            }

            document.Status = DocumentStatus.Indexed;
            document.ChunkCount = chunks.Count;
            document.IndexedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId}", document.Id);
            document.Status = DocumentStatus.Failed;
            document.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);
        }

        return MapToDto(document);
    }

    public async Task<List<DocumentDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Documents.AsNoTracking().OrderByDescending(d => d.UploadedAtUtc).Select(d => MapToDto(d)).ToListAsync(ct);

    public async Task<DocumentDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return doc is null ? null : MapToDto(doc);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var doc = await _db.Documents.FindAsync(new object[] { id }, ct);
        if (doc is null) return;
        // Chunks cascade-delete via FK config
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync(ct);
    }

    private static DocumentDto MapToDto(Document d) => new()
    {
        Id = d.Id,
        Title = d.Title,
        FileName = d.FileName,
        Status = d.Status,
        ErrorMessage = d.ErrorMessage,
        ChunkCount = d.ChunkCount,
        UploadedAtUtc = d.UploadedAtUtc,
        IndexedAtUtc = d.IndexedAtUtc
    };
}
