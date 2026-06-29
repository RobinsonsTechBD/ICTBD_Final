using EmployeeManagement.Application.DTOs.AI;
using EmployeeManagement.Application.DTOs.Rag;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Infrastructure.AI;
using EmployeeManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace EmployeeManagement.Application.Services;

public class RagQueryService : IRagQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly IModelFallbackChatService _chatService;

    private const string NoAnswerMarker = "NOT_FOUND_IN_DOCUMENTS";

    public RagQueryService(ApplicationDbContext db, IEmbeddingService embeddingService, IModelFallbackChatService chatService)
    {
        _db = db;
        _embeddingService = embeddingService;
        _chatService = chatService;
    }

    public async Task<RagAnswerDto> AskAsync(AskQuestionDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Question))
            throw new ArgumentException("Question cannot be empty.");

        // 1. Embed the question with the same model used for chunks (nomic-embed-text)
        var queryEmbedding = await _embeddingService.EmbedAsync(dto.Question, ct);

        // 2. Pull candidate chunks (optionally scoped to specific documents) and rank in-process
        var chunkQuery = _db.DocumentChunks.AsNoTracking()
            .Include(c => c.Document)
            .Where(c => c.Document.Status == DocumentStatus.Indexed);

        if (dto.DocumentIds is { Count: > 0 })
            chunkQuery = chunkQuery.Where(c => dto.DocumentIds.Contains(c.DocumentId));

        var allChunks = await chunkQuery.ToListAsync(ct);
        if (allChunks.Count == 0)
            throw new InvalidOperationException("No indexed documents available to search. Upload and index a document first.");

        var ranked = allChunks
            .Select(c => new
            {
                Chunk = c,
                Score = CosineSimilarity.Compute(queryEmbedding, JsonSerializer.Deserialize<float[]>(c.EmbeddingJson)!)
            })
            .OrderByDescending(x => x.Score)
            .Take(Math.Clamp(dto.TopK, 1, 20))
            .ToList();

        // 3. Build a hallucination-controlled prompt: the model may ONLY answer from the provided context
        var contextBuilder = new StringBuilder();
        for (int i = 0; i < ranked.Count; i++)
        {
            contextBuilder.AppendLine($"[Source {i + 1}: \"{ranked[i].Chunk.Document.Title}\", chunk #{ranked[i].Chunk.ChunkIndex}]");
            contextBuilder.AppendLine(ranked[i].Chunk.Content);
            contextBuilder.AppendLine();
        }

        var systemPrompt =
            "You are a document-grounded assistant. Answer the user's question using ONLY the information " +
            "in the provided sources below. Cite sources inline using their [Source N] label. " +
            $"If the answer is not present in the sources, respond with exactly: {NoAnswerMarker} " +
            "Do not use any outside knowledge. Do not guess.";

        var userPrompt = $"SOURCES:\n{contextBuilder}\n\nQUESTION: {dto.Question}";

        var chatRequest = new ChatCompletionRequestDto
        {
            Temperature = 0.1,   // low temperature — factual retrieval, not creative generation
            Messages = new List<ChatMessageDto>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            }
        };

        var completion = await _chatService.CompleteAsync(chatRequest, ct);
        var answeredFromContext = !completion.Content.Contains(NoAnswerMarker, StringComparison.OrdinalIgnoreCase);

        return new RagAnswerDto
        {
            Answer = answeredFromContext
                ? completion.Content
                : "I couldn't find an answer to that in the uploaded documents.",
            AnsweredFromContext = answeredFromContext,
            ModelUsed = completion.ModelUsed,
            Sources = answeredFromContext
                ? ranked.Select(r => new SourceReferenceDto
                {
                    DocumentId = r.Chunk.DocumentId,
                    DocumentTitle = r.Chunk.Document.Title,
                    ChunkIndex = r.Chunk.ChunkIndex,
                    ExcerptPreview = r.Chunk.Content.Length > 200 ? r.Chunk.Content[..200] + "..." : r.Chunk.Content,
                    SimilarityScore = Math.Round(r.Score, 4)
                }).ToList()
                : new List<SourceReferenceDto>()
        };
    }
}
