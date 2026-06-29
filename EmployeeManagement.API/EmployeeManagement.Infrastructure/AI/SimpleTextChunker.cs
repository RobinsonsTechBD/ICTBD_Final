using EmployeeManagement.Application.Interfaces;

namespace EmployeeManagement.Infrastructure.AI;

/// <summary>
/// Character-based sliding-window chunker. Splits on paragraph/sentence
/// boundaries where possible so chunks stay semantically coherent, rather
/// than cutting mid-sentence. Good enough for contracts/policies/SOPs;
/// swap for a token-aware chunker later if you need tighter token budgets.
/// </summary>
public class SimpleTextChunker : ITextChunker
{
    public List<string> Chunk(string text, int maxChunkChars = 1200, int overlapChars = 150)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        text = text.Trim();
        int position = 0;

        while (position < text.Length)
        {
            int length = Math.Min(maxChunkChars, text.Length - position);
            int end = position + length;

            // Prefer to break at the last paragraph or sentence boundary within this window
            if (end < text.Length)
            {
                int lastBreak = text.LastIndexOfAny(new[] { '\n', '.', '!', '?' }, end - 1, length);
                if (lastBreak > position + (maxChunkChars / 2)) // only use it if it's not too early
                    end = lastBreak + 1;
            }

            var chunk = text[position..end].Trim();
            if (chunk.Length > 0) chunks.Add(chunk);

            if (end >= text.Length) break;
            position = Math.Max(end - overlapChars, position + 1); // overlap, but always make forward progress
        }

        return chunks;
    }
}
