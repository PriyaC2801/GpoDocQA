using GpoDocQA.Api.Models.Domain;
using Microsoft.Extensions.VectorData;
using Npgsql;

namespace GpoDocQA.Api.Services;

public class PostgresChunkVectorStore : IChunkVectorStore
{
    private const string CollectionName = "chunks";

    private readonly VectorStoreCollection<string, DocumentChunk> _collection;
    private readonly string _connectionString;

    public PostgresChunkVectorStore(VectorStore vectorStore, string connectionString)
    {
        _collection = vectorStore.GetCollection<string, DocumentChunk>(CollectionName);
        _connectionString = connectionString;
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        // The connector creates the table and index, but not the pgvector extension
        // its `vector` column type comes from, so enable that first.
        await using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await _collection.EnsureCollectionExistsAsync(cancellationToken);
    }

    public Task UpsertAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken) =>
        _collection.UpsertAsync(chunks, cancellationToken);

    public async Task<bool> ContainsAsync(string key, CancellationToken cancellationToken) =>
        await _collection.GetAsync(key, cancellationToken: cancellationToken) is not null;

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding, string documentId, int topK, CancellationToken cancellationToken)
    {
        var results = new List<ScoredChunk>();

        await foreach (var result in _collection.SearchAsync(
            queryEmbedding,
            topK,
            new VectorSearchOptions<DocumentChunk> { Filter = c => c.DocumentId == documentId },
            cancellationToken))
        {
            results.Add(new ScoredChunk(result.Record, result.Score));
        }

        return results;
    }
}
