using GpoDocQA.Api.Models.Dtos;

namespace GpoDocQA.Api.Services;

public interface IChatService
{
    /// <summary>Runs the full RAG pipeline for one question against one already-indexed document.</summary>
    Task<ChatResponse> AskAsync(Guid documentId, string question, CancellationToken cancellationToken);
}
