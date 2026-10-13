using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

public class ChunkingService : IChunkingService
{
    private const int WordsPerChunk = 300;
    private const int OverlapWords = 50;

    public IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<ExtractedPage> pages)
    {
        var chunks = new List<ChunkDraft>();
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            var words = page.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                continue;
            }

            var start = 0;
            while (start < words.Length)
            {
                var length = Math.Min(WordsPerChunk, words.Length - start);
                var chunkText = string.Join(' ', words, start, length);
                chunks.Add(new ChunkDraft(chunkIndex++, page.PageNumber, chunkText));

                if (start + length >= words.Length)
                {
                    break;
                }

                start += WordsPerChunk - OverlapWords;
            }
        }

        return chunks;
    }
}
