using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

public interface IChunkVectorStore
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken);

    Task UpsertAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken);

    Task<bool> ContainsAsync(string key, CancellationToken cancellationToken);

    /// <summary>Finds the chunks most similar to <paramref name="queryEmbedding"/>, scoped to one document.</summary>
    Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding, string documentId, int topK, CancellationToken cancellationToken);
}
