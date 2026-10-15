using System.Text.RegularExpressions;
using GpoDocQA.Api.Models.Domain;

namespace GpoDocQA.Api.Services;

/// <summary>
/// Packs whole sentences into chunks of roughly <see cref="WordsPerChunk"/> words, repeating the
/// last sentence or two at the start of the next chunk so an idea that straddles a boundary
/// still appears intact in at least one chunk. Chunks never cross a page, so page citations stay exact.
/// </summary>
public partial class ChunkingService : IChunkingService
{
    private const int WordsPerChunk = 300;
    private const int MaxOverlapWords = 60;
    private const int MaxOverlapSentences = 2;

    // A sentence end (. ! ?, optionally followed by a closing quote/bracket) followed by
    // whitespace and something that looks like a sentence start.
    [GeneratedRegex(@"(?<=[.!?][""'”’)\]]?)\s+(?=[\p{Lu}\d""'“‘(])")]
    private static partial Regex SentenceBoundary();

    public IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<ExtractedPage> pages)
    {
        var chunks = new List<ChunkDraft>();
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            var sentences = SplitIntoSentences(page.Text);
            foreach (var text in PackSentences(sentences))
            {
                chunks.Add(new ChunkDraft(chunkIndex++, page.PageNumber, text));
            }
        }

        return chunks;
    }

    private static List<string> SplitIntoSentences(string pageText)
    {
        var sentences = new List<string>();

        // Blank lines separate layout blocks (headings, paragraphs, table cells), which are
        // sentence boundaries even when they carry no end punctuation.
        foreach (var block in Regex.Split(pageText, @"\n\s*\n"))
        {
            foreach (var raw in SentenceBoundary().Split(block))
            {
                var sentence = WhitespaceRun().Replace(raw, " ").Trim();
                if (sentence.Length == 0)
                {
                    continue;
                }

                // A "sentence" longer than a whole chunk (e.g. a table dump with no punctuation)
                // is cut by words so it can still be packed.
                var words = sentence.Split(' ');
                for (var i = 0; i < words.Length; i += WordsPerChunk)
                {
                    sentences.Add(string.Join(' ', words.Skip(i).Take(WordsPerChunk)));
                }
            }
        }

        return sentences;
    }

    private static IEnumerable<string> PackSentences(List<string> sentences)
    {
        var current = new List<string>();
        var currentWords = 0;
        var newSentencesInCurrent = 0; // sentences in `current` that are not just carried-over overlap

        foreach (var sentence in sentences)
        {
            var sentenceWords = CountWords(sentence);

            if (currentWords + sentenceWords > WordsPerChunk && newSentencesInCurrent > 0)
            {
                yield return string.Join(' ', current);

                current = TakeOverlap(current);
                currentWords = current.Sum(CountWords);
                newSentencesInCurrent = 0;

                if (currentWords + sentenceWords > WordsPerChunk)
                {
                    current.Clear();
                    currentWords = 0;
                }
            }

            current.Add(sentence);
            currentWords += sentenceWords;
            newSentencesInCurrent++;
        }

        if (newSentencesInCurrent > 0)
        {
            yield return string.Join(' ', current);
        }
    }

    private static List<string> TakeOverlap(List<string> chunkSentences)
    {
        var overlap = new List<string>();
        var words = 0;

        for (var i = chunkSentences.Count - 1; i >= 0 && overlap.Count < MaxOverlapSentences; i--)
        {
            var sentenceWords = CountWords(chunkSentences[i]);
            if (words + sentenceWords > MaxOverlapWords)
            {
                break;
            }

            overlap.Insert(0, chunkSentences[i]);
            words += sentenceWords;
        }

        return overlap;
    }

    private static int CountWords(string text) => text.Count(c => c == ' ') + 1;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
