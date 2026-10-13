using GpoDocQA.Api.Models.Domain;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace GpoDocQA.Api.Services;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public IReadOnlyList<ExtractedPage> ExtractPages(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var pages = new List<ExtractedPage>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            pages.Add(new ExtractedPage(page.Number, text));
        }

        return pages;
    }
}
