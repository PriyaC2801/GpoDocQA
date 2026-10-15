using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

public class DocumentIndexingService : IDocumentIndexingService
{
    // Caps how many embedding calls run at once, so a large report doesn't fire
    // hundreds of simultaneous requests at the Gemini API and hit rate limits.
    private const int MaxConcurrentEmbeddingCalls = 4;

    private readonly IPdfTextExtractor _textExtractor;
    private readonly IChunkingService _chunkingService;
    private readonly IGeminiService _geminiService;
    private readonly IChunkVectorStore _vectorStore;
    private readonly ILogger<DocumentIndexingService> _logger;

    public DocumentIndexingService(
        IPdfTextExtractor textExtractor,
        IChunkingService chunkingService,
        IGeminiService geminiService,
        IChunkVectorStore vectorStore,
        ILogger<DocumentIndexingService> logger)
    {
        _textExtractor = textExtractor;
        _chunkingService = chunkingService;
        _geminiService = geminiService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    // The free tier caps embeddings per minute, and a full document is a burst of one call per chunk.
    // GeminiService only retries once, briefly; here we can afford to wait out the minute window.
    private const int MaxEmbeddingAttempts = 6;
    private static readonly TimeSpan EmbeddingBackoffStep = TimeSpan.FromSeconds(15);

    private async Task<ReadOnlyMemory<float>> EmbedWithBackoffAsync(string text, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await _geminiService.GenerateEmbeddingAsync(text, EmbeddingPurpose.Document, cancellationToken);
            }
            catch (GeminiQuotaExceededException) when (attempt < MaxEmbeddingAttempts)
            {
                _logger.LogWarning("Embedding rate limit hit; waiting before retry {Attempt}/{Max}", attempt, MaxEmbeddingAttempts - 1);
                await Task.Delay(EmbeddingBackoffStep * attempt, cancellationToken);
            }
        }
    }

    public async Task IndexAsync(DocumentRecord record, CancellationToken cancellationToken)
    {
        record.Status = DocumentStatus.Processing;

        try
        {
            // PdfPig parsing is CPU-bound, not I/O-bound, so it runs on a thread-pool
            // thread instead of blocking the request thread that called us.
            var pages = await Task.Run(() => _textExtractor.ExtractPages(record.FilePath), cancellationToken);

            if (pages.All(p => string.IsNullOrWhiteSpace(p.Text)))
            {
                record.Status = DocumentStatus.Failed;
                record.ErrorMessage = "No extractable text was found in this PDF. " +
                    "It may be a scanned/image-only document, which this project does not OCR.";
                _logger.LogWarning("Document {DocumentId} has no extractable text", record.Id);
                return;
            }

            record.Pages = pages;
            record.PageCount = pages.Count;

            var chunkDrafts = _chunkingService.Chunk(pages);
            var documentIdText = record.Id.ToString();

            // Chunks are upserted all at once below, so if the first one is already in
            // the (persistent) vector store, this document was fully indexed on an
            // earlier run and there's no need to pay for the embedding calls again.
            await _vectorStore.EnsureCreatedAsync(cancellationToken);
            if (chunkDrafts.Count > 0 &&
                await _vectorStore.ContainsAsync($"{documentIdText}_{chunkDrafts[0].ChunkIndex}", cancellationToken))
            {
                record.ChunkCount = chunkDrafts.Count;
                record.Status = DocumentStatus.Ready;
                _logger.LogInformation(
                    "Document {DocumentId} is already indexed ({ChunkCount} chunks); skipping embedding",
                    record.Id, chunkDrafts.Count);
                return;
            }

            var chunks = new DocumentChunk[chunkDrafts.Count];

            await Parallel.ForEachAsync(
                Enumerable.Range(0, chunkDrafts.Count),
                new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentEmbeddingCalls, CancellationToken = cancellationToken },
                async (i, ct) =>
                {
                    var draft = chunkDrafts[i];
                    var embedding = await EmbedWithBackoffAsync(draft.Text, ct);

                    chunks[i] = new DocumentChunk
                    {
                        Key = $"{documentIdText}_{draft.ChunkIndex}",
                        DocumentId = documentIdText,
                        ChunkIndex = draft.ChunkIndex,
                        PageNumber = draft.PageNumber,
                        Text = draft.Text,
                        Embedding = embedding
                    };
                });

            await _vectorStore.UpsertAsync(chunks, cancellationToken);

            record.ChunkCount = chunks.Length;
            record.Status = DocumentStatus.Ready;

            _logger.LogInformation(
                "Indexed document {DocumentId}: {PageCount} pages -> {ChunkCount} chunks embedded and stored",
                record.Id, pages.Count, chunks.Length);
        }
        catch (Exception ex)
        {
            record.Status = DocumentStatus.Failed;
            record.ErrorMessage = "Could not process this document (text extraction or embedding failed).";
            _logger.LogError(ex, "Indexing failed for document {DocumentId}", record.Id);
        }
    }
}
