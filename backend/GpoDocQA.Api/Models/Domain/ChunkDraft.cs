namespace GpoDocQA.Api.Models.Domain;

/// <summary>A chunk of page text before it has an embedding or a document id attached.</summary>
public record ChunkDraft(int ChunkIndex, int PageNumber, string Text);
