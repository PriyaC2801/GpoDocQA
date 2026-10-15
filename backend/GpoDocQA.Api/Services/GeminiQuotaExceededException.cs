namespace GpoDocQA.Api.Services;

/// <summary>
/// Gemini rejected a request because the rate limit or daily quota was hit (HTTP 429 /
/// RESOURCE_EXHAUSTED). Distinct from other failures so the API can answer 429 instead of 500.
/// </summary>
public class GeminiQuotaExceededException : Exception
{
    public GeminiQuotaExceededException(Exception inner)
        : base("The Gemini API quota or rate limit has been reached.", inner)
    {
    }

    public static bool IsQuotaError(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("429", StringComparison.Ordinal) ||
                e.Message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
