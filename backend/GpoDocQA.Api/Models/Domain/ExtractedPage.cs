namespace GpoDocQA.Api.Models.Domain;

/// <summary>
/// The raw text of one page of a PDF, before chunking (Phase 6).
/// </summary>
public record ExtractedPage(int PageNumber, string Text);
