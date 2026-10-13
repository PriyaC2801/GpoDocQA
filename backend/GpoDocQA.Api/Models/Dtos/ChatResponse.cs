namespace GpoDocQA.Api.Models.Dtos;

public record ChatResponse(
    Guid DocumentId,
    string Question,
    string Answer,
    bool HasEvidence,
    IReadOnlyList<SourceExcerpt> Sources);
