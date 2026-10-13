namespace GpoDocQA.Api.Configuration;

public class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    /// <summary>The PDF the app answers questions about, relative to the content root.</summary>
    public string StaticDocumentPath { get; set; } = "Documents/GPO-Annual-report.pdf";
}
