using Microsoft.Extensions.AI;

namespace GpoDocQA.Api.Services;

public class GeminiService : IGeminiService
{
    // The Gemini SDK doesn't reliably fail fast on quota/rate-limit errors, so without
    // this a stuck call blocks the request indefinitely instead of surfacing an error.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    // A per-minute rate limit often clears within seconds, so one short retry is worthwhile.
    // A daily quota won't, and then the second 429 is surfaced as GeminiQuotaExceededException.
    private static readonly TimeSpan QuotaRetryDelay = TimeSpan.FromSeconds(3);

    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChatClient _chatClient;

    public GeminiService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, IChatClient chatClient)
    {
        _embeddingGenerator = embeddingGenerator;
        _chatClient = chatClient;
    }

    public Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken) =>
        CallWithTimeoutAsync("embedding", async token =>
        {
            var embeddings = await _embeddingGenerator.GenerateAsync([text], cancellationToken: token);
            return embeddings[0].Vector;
        }, cancellationToken);

    public Task<string> GenerateAnswerAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        List<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];

        return CallWithTimeoutAsync("chat", async token =>
        {
            var response = await _chatClient.GetResponseAsync(
                messages, new ChatOptions { Temperature = 0.1f }, token);
            return response.Text;
        }, cancellationToken);
    }

    private static async Task<T> CallWithTimeoutAsync<T>(
        string operation, Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await AttemptAsync(operation, call, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && GeminiQuotaExceededException.IsQuotaError(ex))
        {
            await Task.Delay(QuotaRetryDelay, cancellationToken);

            try
            {
                return await AttemptAsync(operation, call, cancellationToken);
            }
            catch (Exception retryEx) when (retryEx is not OperationCanceledException && GeminiQuotaExceededException.IsQuotaError(retryEx))
            {
                throw new GeminiQuotaExceededException(retryEx);
            }
        }
    }

    private static async Task<T> AttemptAsync<T>(
        string operation, Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RequestTimeout);

        try
        {
            return await call(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Gemini {operation} request timed out after {RequestTimeout.TotalSeconds}s.");
        }
    }
}
