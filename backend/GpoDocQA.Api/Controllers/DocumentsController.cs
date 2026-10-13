using GpoDocQA.Api.Models.Dtos;
using GpoDocQA.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GpoDocQA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentStore _documentStore;

    public DocumentsController(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    /// <summary>The bundled document the app answers questions about.</summary>
    [HttpGet("current")]
    public ActionResult<DocumentResponse> GetCurrent() => GetById(StaticDocumentLoader.StaticDocumentId);

    [HttpGet("{id:guid}")]
    public ActionResult<DocumentResponse> GetById(Guid id)
    {
        if (!_documentStore.TryGet(id, out var record))
        {
            return Problem(
                title: "Document not found.",
                detail: $"No document with id '{id}' was found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(ToResponse(record));
    }

    private static DocumentResponse ToResponse(Models.Domain.DocumentRecord record) => new(
        record.Id, record.FileName, record.Status,
        record.PageCount, record.ChunkCount, record.ErrorMessage);
}
