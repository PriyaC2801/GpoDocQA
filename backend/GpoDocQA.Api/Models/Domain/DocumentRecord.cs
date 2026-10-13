namespace GpoDocQA.Api.Models.Domain;

public class DocumentRecord
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required long SizeBytes { get; init; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public int? PageCount { get; set; }
    public int? ChunkCount { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Per-page extracted text, filled in by <see cref="IDocumentIndexingService"/>. Empty until processed.</summary>
    public IReadOnlyList<ExtractedPage> Pages { get; set; } = [];
}
