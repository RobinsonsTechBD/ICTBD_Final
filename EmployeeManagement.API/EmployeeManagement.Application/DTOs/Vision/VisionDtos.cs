namespace EmployeeManagement.Application.DTOs.Vision;

public enum VisionTaskType
{
    GeneralDescription = 1,    // "describe what's in this image"
    DocumentOCR = 2,           // extract text from a photo of a document
    AttendanceDevicePhoto = 3, // analyse a photo of a biometric/RFID device screen
    FaceDetectionCheck = 4     // check if a face is visible (not identification — just presence detection)
}

public class VisionAnalysisRequestDto
{
    /// <summary>Base64-encoded image data (without the data:image/xxx;base64, prefix).</summary>
    public string ImageBase64 { get; set; } = default!;

    public string MimeType { get; set; } = "image/jpeg";    // "image/jpeg" | "image/png" | "image/webp"
    public VisionTaskType TaskType { get; set; } = VisionTaskType.GeneralDescription;

    /// <summary>Optional free-text question about the image (overrides the default task prompt if provided).</summary>
    public string? CustomPrompt { get; set; }
}

public class VisionAnalysisResponseDto
{
    public string Analysis { get; set; } = default!;
    public VisionTaskType TaskType { get; set; }
    public string ModelUsed { get; set; } = default!;
    public double ElapsedSeconds { get; set; }
    public string? ExtractedText { get; set; }      // populated for DocumentOCR and AttendanceDevicePhoto tasks
}

/// <summary>
/// Request to index an image directly into the RAG vector store as a document —
/// LLaVA generates a textual description, which is then chunked and embedded
/// just like an uploaded text document, making images searchable via Phase C.
/// </summary>
public class VisionIndexRequestDto
{
    public string ImageBase64 { get; set; } = default!;
    public string MimeType { get; set; } = "image/jpeg";
    public string DocumentTitle { get; set; } = default!;
    public string FileName { get; set; } = default!;
}

public class VisionIndexResponseDto
{
    public int DocumentId { get; set; }
    public string GeneratedDescription { get; set; } = default!;
    public int ChunkCount { get; set; }
}