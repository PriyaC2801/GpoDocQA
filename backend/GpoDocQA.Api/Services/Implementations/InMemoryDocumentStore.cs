using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

public class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<Guid, DocumentRecord> _documents = new();

    public void Add(DocumentRecord record) => _documents[record.Id] = record;

    public bool TryGet(Guid id, [NotNullWhen(true)] out DocumentRecord? record) =>
        _documents.TryGetValue(id, out record);
}
