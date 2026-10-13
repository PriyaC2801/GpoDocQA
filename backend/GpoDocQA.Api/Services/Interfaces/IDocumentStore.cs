using System.Diagnostics.CodeAnalysis;
using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

/// <summary>
/// In-memory registry of documents and their processing status.
/// Phase 10 discusses swapping this for real persistence (e.g. a database).
/// </summary>
public interface IDocumentStore
{
    void Add(DocumentRecord record);

    bool TryGet(Guid id, [NotNullWhen(true)] out DocumentRecord? record);
}
