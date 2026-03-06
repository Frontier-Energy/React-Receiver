using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("inspections")]
public sealed class InspectionsController : ControllerBase
{
    private readonly IInspectionRequestHandler _inspectionRequestHandler;
    private readonly IReceiveInspectionRequestParser _receiveInspectionRequestParser;
    private readonly IInspectionQueryService _inspectionQueryService;

    public InspectionsController(
        IInspectionRequestHandler inspectionRequestHandler,
        IReceiveInspectionRequestParser receiveInspectionRequestParser,
        IInspectionQueryService inspectionQueryService)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
        _receiveInspectionRequestParser = receiveInspectionRequestParser;
        _inspectionQueryService = inspectionQueryService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromForm] string? payload,
        [FromForm] IFormFile[]? files)
    {
        if (!_receiveInspectionRequestParser.TryParseFormRequest(payload, files, out var request))
        {
            return BadRequest("Invalid payload JSON.");
        }

        var response = await _inspectionRequestHandler.SaveRequestAsync(request, HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<GetInspectionResponse>> GetInspection([FromRoute] string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("SessionId is required.");
        }

        var response = await _inspectionQueryService.GetInspectionAsync(sessionId, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{sessionId}/files/{fileName}")]
    public async Task<IActionResult> GetFile([FromRoute] string sessionId, [FromRoute] string fileName)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("SessionId and fileName are required.");
        }

        var response = await _inspectionQueryService.GetFileAsync(sessionId, fileName, HttpContext.RequestAborted);
        return response is null
            ? NotFound()
            : File(response.Content, response.ContentType, response.FileName);
    }
}
