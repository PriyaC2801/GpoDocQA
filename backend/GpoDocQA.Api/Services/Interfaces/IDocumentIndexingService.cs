using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

/// <summary>
/// Turns a PDF into retrievable knowledge: extract text (Phase 5),
/// then chunk and embed it (Phase 6). Mutates the given <see cref="DocumentRecord"/>'s
/// Status/PageCount/Pages/ErrorMessage in place as processing proceeds.
/// </summary>
public interface IDocumentIndexingService
{
    Task IndexAsync(DocumentRecord record, CancellationToken cancellationToken);
}
