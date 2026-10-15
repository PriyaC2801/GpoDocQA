using GpoDocQA.Api.Models.Domain;
using Microsoft.Extensions.VectorData;
using System.Text.RegularExpressions;
using Npgsql;

namespace GpoDocQA.Api.Services;

public partial class PostgresChunkVectorStore : IChunkVectorStore
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

        // Backs SearchKeywordsAsync: the expression must match the one used in its queries.
        await using var indexConnection = new NpgsqlConnection(_connectionString);
        await indexConnection.OpenAsync(cancellationToken);
        await using var indexCommand = new NpgsqlCommand(
            "CREATE INDEX IF NOT EXISTS chunks_text_fts ON chunks USING gin (to_tsvector('english', \"Text\"))", indexConnection);
        await indexCommand.ExecuteNonQueryAsync(cancellationToken);
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

    public async Task<IReadOnlyList<ScoredChunk>> SearchKeywordsAsync(
        string question, string documentId, int topK, CancellationToken cancellationToken)
    {
        // Questions are natural language, so match ANY meaningful term and let ts_rank_cd
        // put chunks matching more (and closer-together) terms first. Words are reduced to
        // letters/digits, which also makes the websearch syntax safe against user input.
        var terms = KeywordToken().Matches(question)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(t => t is not "or" and not "and")
            .Distinct()
            .ToList();

        var results = new List<ScoredChunk>();
        if (terms.Count == 0)
        {
            return results;
        }

        const string sql = """
            SELECT "Key", "DocumentId", "ChunkIndex", "PageNumber", "Text",
                   ts_rank_cd(to_tsvector('english', "Text"), q.query) AS rank
            FROM chunks, (SELECT websearch_to_tsquery('english', @query) AS query) AS q
            WHERE "DocumentId" = @documentId AND to_tsvector('english', "Text") @@ q.query
            ORDER BY rank DESC
            LIMIT @limit
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("query", string.Join(" or ", terms));
        command.Parameters.AddWithValue("documentId", documentId);
        command.Parameters.AddWithValue("limit", topK);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ScoredChunk(
                new DocumentChunk
                {
                    Key = reader.GetString(0),
                    DocumentId = reader.GetString(1),
                    ChunkIndex = reader.GetInt32(2),
                    PageNumber = reader.GetInt32(3),
                    Text = reader.GetString(4)
                },
                reader.GetFloat(5)));
        }

        return results;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex KeywordToken();
}
