using GpoDocQA.Api.Models.Domain;
using GpoDocQA.Api.Models.Dtos;
using GpoDocQA.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GpoDocQA.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IDocumentStore _documentStore;

    public ChatController(IChatService chatService, IDocumentStore documentStore)
    {
        _chatService = chatService;
        _documentStore = documentStore;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<ChatResponse>> Ask(ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Problem(
                title: "Question is required.",
                detail: "The 'question' field cannot be empty.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!_documentStore.TryGet(request.DocumentId, out var document))
        {
            return Problem(
                title: "Document not found.",
                detail: $"No document with id '{request.DocumentId}' was found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (document.Status != DocumentStatus.Ready)
        {
            return Problem(
                title: "Document is not ready.",
                detail: $"This document's status is '{document.Status}'. Wait for it to finish processing before asking questions.",
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            var response = await _chatService.AskAsync(request.DocumentId, request.Question, cancellationToken);
            return Ok(response);
        }
        catch (GeminiQuotaExceededException)
        {
            return Problem(
                title: "Gemini quota reached.",
                detail: "The Gemini API quota or rate limit has been reached (the free tier allows only a few chat requests per day). Please try again later.",
                statusCode: StatusCodes.Status429TooManyRequests);
        }
    }
}
