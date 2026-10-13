using Microsoft.Extensions.AI;

namespace GpoDocQA.Api.Services;

public class GeminiService : IGeminiService
{
    // The Gemini SDK doesn't reliably fail fast on quota/rate-limit errors, so without
    // this a stuck call blocks the request indefinitely instead of surfacing an error.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChatClient _chatClient;

    public GeminiService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, IChatClient chatClient)
    {
        _embeddingGenerator = embeddingGenerator;
        _chatClient = chatClient;
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var embeddings = await _embeddingGenerator.GenerateAsync([text], cancellationToken: cts.Token);
            return embeddings[0].Vector;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Gemini embedding request timed out after {RequestTimeout.TotalSeconds}s.");
        }
    }

    public async Task<string> GenerateAnswerAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        List<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeout);

        try
        {
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cts.Token);
            return response.Text;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Gemini chat request timed out after {RequestTimeout.TotalSeconds}s.");
        }
    }
}
