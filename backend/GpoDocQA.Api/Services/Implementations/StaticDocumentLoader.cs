using GpoDocQA.Api.Configuration;
using GpoDocQA.Api.Models.Domain;
using Microsoft.Extensions.Options;

namespace GpoDocQA.Api.Services;

/// <summary>
/// Registers the bundled PDF in the document store at startup and indexes it in the
/// background, so the API is reachable immediately while embedding is still running.
/// </summary>
public class StaticDocumentLoader : BackgroundService
{
    /// <summary>
    /// Fixed id so the document's chunks in the persistent vector store can be found
    /// again after a restart instead of being re-embedded every time.
    /// </summary>
    public static readonly Guid StaticDocumentId = new("6f1c2d4e-3b5a-4c7d-9e8f-0a1b2c3d4e5f");

    private readonly IDocumentStore _documentStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _filePath;
    private readonly ILogger<StaticDocumentLoader> _logger;

    public StaticDocumentLoader(
        IDocumentStore documentStore,
        IServiceScopeFactory scopeFactory,
        IOptions<DocumentStorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<StaticDocumentLoader> logger)
    {
        _documentStore = documentStore;
        _scopeFactory = scopeFactory;
        _filePath = Path.Combine(environment.ContentRootPath, options.Value.StaticDocumentPath);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fileInfo = new FileInfo(_filePath);

        var record = new DocumentRecord
        {
            Id = StaticDocumentId,
            FileName = fileInfo.Name,
            FilePath = _filePath,
            SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            Status = DocumentStatus.Pending
        };

        _documentStore.Add(record);

        if (!fileInfo.Exists)
        {
            record.Status = DocumentStatus.Failed;
            record.ErrorMessage = "The document file could not be found on the server.";
            _logger.LogError("Static document not found at {FilePath}", _filePath);
            return;
        }

        // IDocumentIndexingService is scoped, and a hosted service is a singleton,
        // so it needs its own scope.
        using var scope = _scopeFactory.CreateScope();
        var indexingService = scope.ServiceProvider.GetRequiredService<IDocumentIndexingService>();
        await indexingService.IndexAsync(record, stoppingToken);
    }
}
