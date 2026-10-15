namespace GpoDocQA.Api.Services;

/// <summary>
/// Gemini embeddings are trained asymmetrically: documents and the questions that search
/// them should be embedded with different task types for best retrieval quality.
/// </summary>
public enum EmbeddingPurpose
{
    Document,
    Query
}

/// <summary>
/// All calls to the Gemini API go through this interface, so controllers and other
/// services never depend on Semantic Kernel / Microsoft.Extensions.AI types directly.
/// </summary>
public interface IGeminiService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken);

    Task<string> GenerateAnswerAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}
