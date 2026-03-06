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
        [FromForm] ReceiveInspectionFormRequest request)
    {
        if (!_receiveInspectionRequestParser.TryParseFormRequest(request.Payload, request.Files, out var parsedRequest))
        {
            return BadRequest("Invalid payload JSON.");
        }

        var response = await _inspectionRequestHandler.SaveRequestAsync(parsedRequest, HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<GetInspectionResponse>> GetInspection([FromRoute] GetInspectionRequest request)
    {
        var response = await _inspectionQueryService.GetInspectionAsync(request.SessionId!, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{sessionId}/files/{fileName}")]
    public async Task<IActionResult> GetFile([FromRoute] GetInspectionFileRequest request)
    {
        var response = await _inspectionQueryService.GetFileAsync(
            request.SessionId!,
            request.FileName!,
            HttpContext.RequestAborted);
        return response is null
            ? NotFound()
            : File(response.Content, response.ContentType, response.FileName);
    }
}
