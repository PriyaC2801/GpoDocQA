using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Models.Dtos;

public record DocumentResponse(
    Guid DocumentId,
    string FileName,
    DocumentStatus Status,
    int? PageCount,
    int? ChunkCount,
    string? ErrorMessage);
