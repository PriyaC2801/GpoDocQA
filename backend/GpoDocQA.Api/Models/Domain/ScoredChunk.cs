namespace GpoDocQA.Api.Models.Domain;

/// <summary>A chunk returned from a vector search, with how closely it matched the query.</summary>
public record ScoredChunk(DocumentChunk Chunk, double? Score);
