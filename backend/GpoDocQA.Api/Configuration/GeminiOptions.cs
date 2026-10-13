namespace GpoDocQA.Api.Configuration;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Set via User Secrets locally (dotnet user-secrets set "Gemini:ApiKey" "..."), never in appsettings.json.</summary>
    public string ApiKey { get; set; } = "";

    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    public int EmbeddingDimensions { get; set; } = 768;

    public string ChatModel { get; set; } = "gemini-3.5-flash-lite";
}
