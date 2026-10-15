using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GpoDocQA.Api.Configuration;
using Microsoft.Extensions.AI;

namespace GpoDocQA.Api.Services;

public class GeminiService : IGeminiService
{
    private const string EmbedEndpointFormat = "https://generativelanguage.googleapis.com/v1beta/models/{0}:embedContent";

    // The Gemini SDK doesn't reliably fail fast on quota/rate-limit errors, so without
    // this a stuck call blocks the request indefinitely instead of surfacing an error.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    // A per-minute rate limit often clears within seconds, so one short retry is worthwhile.
    // A daily quota won't, and then the second 429 is surfaced as GeminiQuotaExceededException.
    private static readonly TimeSpan QuotaRetryDelay = TimeSpan.FromSeconds(3);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiOptions _options;
    private readonly IChatClient _chatClient;

    public GeminiService(IHttpClientFactory httpClientFactory, GeminiOptions options, IChatClient chatClient)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _chatClient = chatClient;
    }

    // Embeddings go straight to the REST endpoint (not the Microsoft.Extensions.AI wrapper) because
    // that is the only way to set taskType and outputDimensionality explicitly.
    public Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text, EmbeddingPurpose purpose, CancellationToken cancellationToken) =>
        CallWithTimeoutAsync("embedding", async token =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, string.Format(EmbedEndpointFormat, _options.EmbeddingModel))
            {
                Content = JsonContent.Create(new EmbedRequest(
                    new EmbedContent([new EmbedPart(text)]),
                    purpose == EmbeddingPurpose.Query ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT",
                    _options.EmbeddingDimensions))
            };
            request.Headers.Add("x-goog-api-key", _options.ApiKey);

            using var response = await _httpClientFactory.CreateClient().SendAsync(request, token);
            if (!response.IsSuccessStatusCode)
            {
                // Status code goes in the message so GeminiQuotaExceededException.IsQuotaError can see a 429.
                throw new HttpRequestException(
                    $"Gemini embedding request failed with status {(int)response.StatusCode} ({response.StatusCode}).",
                    inner: null,
                    response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: token)
                ?? throw new InvalidOperationException("Gemini returned an empty embedding response.");
            return (ReadOnlyMemory<float>)body.Embedding.Values;
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

    private record EmbedPart([property: JsonPropertyName("text")] string Text);

    private record EmbedContent([property: JsonPropertyName("parts")] EmbedPart[] Parts);

    private record EmbedRequest(
        [property: JsonPropertyName("content")] EmbedContent Content,
        [property: JsonPropertyName("taskType")] string TaskType,
        [property: JsonPropertyName("outputDimensionality")] int OutputDimensionality);

    private record EmbedValues([property: JsonPropertyName("values")] float[] Values);

    private record EmbedResponse([property: JsonPropertyName("embedding")] EmbedValues Embedding);
}
