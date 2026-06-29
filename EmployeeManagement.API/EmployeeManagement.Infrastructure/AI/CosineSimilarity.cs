namespace EmployeeManagement.Infrastructure.AI;

/// <summary>
/// In-process cosine similarity over float[] embeddings. Embeddings are
/// pulled from SQL as JSON and deserialized, then compared here in memory —
/// fine for hundreds to low-thousands of chunks. If your corpus grows large
/// enough that this becomes a bottleneck, the migration path is either
/// SQL Server 2025's native VECTOR + VECTOR_DISTANCE, or an external vector
/// DB (Pinecone/Qdrant) behind the same IEmbeddingService/chunk-retrieval seam.
/// </summary>
public static class CosineSimilarity
{
    public static double Compute(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new InvalidOperationException($"Embedding dimension mismatch: {a.Length} vs {b.Length}. Did the embedding model change?");

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
