namespace GpoDocQA.Api.Services;

/// <summary>
/// All calls to the Gemini API go through this interface, so controllers and other
/// services never depend on Semantic Kernel / Microsoft.Extensions.AI types directly.
/// </summary>
public interface IGeminiService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);

    Task<string> GenerateAnswerAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
