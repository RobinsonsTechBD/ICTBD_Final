using EmployeeManagement.Application.DTOs.Vision;

namespace EmployeeManagement.Application.Interfaces;

/// <summary>
/// Sends an image + prompt to a vision-capable model in the Phase B fallback
/// chain (RequireVision=true filters to only LLaVA / VL models).
/// </summary>
public interface IVisionAnalysisService
{
    Task<VisionAnalysisResponseDto> AnalyseAsync(VisionAnalysisRequestDto request, CancellationToken ct = default);
}

/// <summary>
/// Generates a textual description of an image via LLaVA, then indexes
/// that text into the Phase C RAG vector store — making image content
/// searchable alongside uploaded documents via /api/rag/ask.
/// </summary>
public interface IVisionIndexService
{
    Task<VisionIndexResponseDto> IndexImageAsync(VisionIndexRequestDto request, int uploadedByEmployeeId, CancellationToken ct = default);
}