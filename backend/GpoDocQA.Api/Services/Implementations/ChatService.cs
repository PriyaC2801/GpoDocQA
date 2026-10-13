using GpoDocQA.Api.Models.Dtos;
using GpoDocQA.Api.Prompts;

namespace GpoDocQA.Api.Services;

public class ChatService : IChatService
{
    private const int TopK = 5;

    private const string NoEvidenceAnswer = "The document does not provide enough information to answer this question.";

    private readonly IGeminiService _geminiService;
    private readonly IChunkVectorStore _vectorStore;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IGeminiService geminiService, IChunkVectorStore vectorStore, ILogger<ChatService> logger)
    {
        _geminiService = geminiService;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<ChatResponse> AskAsync(Guid documentId, string question, CancellationToken cancellationToken)
    {
        var documentIdText = documentId.ToString();

        var questionEmbedding = await _geminiService.GenerateEmbeddingAsync(question, cancellationToken);
        var matches = await _vectorStore.SearchAsync(questionEmbedding, documentIdText, TopK, cancellationToken);

        // We deliberately do NOT gate on a raw similarity-score threshold here: testing
        // showed cosine-similarity scores for a clearly unrelated question can be as high
        // as, or higher than, scores for a genuinely relevant one (short text embeddings
        // have a high similarity "baseline" regardless of topic). A numeric cutoff would
        // both reject good matches and let bad ones through. The real check is the model's
        // own "EVIDENCE: YES/NO" judgment below, made with the full question and context.
        if (matches.Count == 0)
        {
            _logger.LogInformation("No chunks found at all for document {DocumentId}", documentId);
            return new ChatResponse(documentId, question, NoEvidenceAnswer, HasEvidence: false, Sources: []);
        }

        var userPrompt = RagPromptBuilder.BuildUserPrompt(question, matches);
        var rawAnswer = await _geminiService.GenerateAnswerAsync(RagPromptBuilder.SystemPrompt, userPrompt, cancellationToken);
        var parsed = RagResponseParser.Parse(rawAnswer);

        // Sources always come from OUR retrieval, never from what the model claims it
        // used - this is what guarantees citations can't be invented or mis-numbered.
        var sources = parsed.HasEvidence
            ? matches.Select(m => new SourceExcerpt(m.Chunk.PageNumber, m.Chunk.Text)).ToList()
            : [];

        return new ChatResponse(
            documentId,
            question,
            parsed.HasEvidence ? parsed.Answer : NoEvidenceAnswer,
            parsed.HasEvidence,
            sources);
    }
}
