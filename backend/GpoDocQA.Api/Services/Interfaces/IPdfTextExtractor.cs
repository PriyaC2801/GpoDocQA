using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

public interface IPdfTextExtractor
{
    /// <summary>
    /// Reads a PDF from disk and returns the text of each page, in page order,
    /// with 1-based page numbers so they match what a human reading the PDF would cite.
    /// </summary>
    IReadOnlyList<ExtractedPage> ExtractPages(string filePath);
}
