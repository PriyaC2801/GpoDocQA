using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

/// <summary>
/// Merges several ranked result lists into one using Reciprocal Rank Fusion: each chunk scores
/// the sum of 1 / (k + rank) over the lists it appears in. It uses only ranks, so the unrelated
/// score scales of vector similarity and keyword rank never have to be reconciled.
/// </summary>
public static class ReciprocalRankFusion
{
    // 60 is the constant from the original RRF paper and the usual default.
    private const int RankConstant = 60;

    public static IReadOnlyList<ScoredChunk> Fuse(int take, params IReadOnlyList<ScoredChunk>[] rankedLists)
    {
        var scores = new Dictionary<string, (DocumentChunk Chunk, double Score)>();

        foreach (var list in rankedLists)
        {
            for (var rank = 0; rank < list.Count; rank++)
            {
                var chunk = list[rank].Chunk;
                var contribution = 1.0 / (RankConstant + rank + 1);
                scores[chunk.Key] = scores.TryGetValue(chunk.Key, out var existing)
                    ? (existing.Chunk, existing.Score + contribution)
                    : (chunk, contribution);
            }
        }

        return scores.Values
            .OrderByDescending(s => s.Score)
            .Take(take)
            .Select(s => new ScoredChunk(s.Chunk, s.Score))
            .ToList();
    }
}
