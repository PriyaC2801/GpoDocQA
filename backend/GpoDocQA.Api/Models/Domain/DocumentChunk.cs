using Microsoft.Extensions.VectorData;

namespace GpoDocQA.Api.Models.Domain;

/// <summary>
/// One chunk of a document's text, together with its embedding vector.
/// This is both the chunking output and the record persisted in the vector store -
/// the attributes below tell the Postgres (pgvector) connector how to map it to a table.
/// </summary>
public class DocumentChunk
{
    [VectorStoreKey]
    public string Key { get; set; } = "";

    // Indexed because every search filters on it.
    [VectorStoreData(IsIndexed = true)]
    public string DocumentId { get; set; } = "";

    [VectorStoreData]
    public int ChunkIndex { get; set; }

    [VectorStoreData]
    public int PageNumber { get; set; }

    [VectorStoreData]
    public string Text { get; set; } = "";

    // Dimensions must be a compile-time constant here, so it has to match
    // GeminiOptions.EmbeddingDimensions by hand if that ever changes.
    [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineDistance, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
