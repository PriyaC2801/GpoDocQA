using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

public interface IChunkingService
{
    /// <summary>
    /// Splits page text into overlapping word-windows. Chunks never cross a page
    /// boundary, so every chunk can be cited with an exact, unambiguous page number.
    /// </summary>
    IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<ExtractedPage> pages);
}
