using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Inspections;
using React_Receiver.Handlers;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("inspections")]
public sealed class InspectionsController : ControllerBase
{
    private readonly IInspectionApplicationService _inspectionApplicationService;
    private readonly IReceiveInspectionRequestParser _receiveInspectionRequestParser;

    public InspectionsController(
        IInspectionApplicationService inspectionApplicationService,
        IReceiveInspectionRequestParser receiveInspectionRequestParser)
    {
        _inspectionApplicationService = inspectionApplicationService;
        _receiveInspectionRequestParser = receiveInspectionRequestParser;
    }

    [HttpPost]
    [HttpPost("/QHVAC/ReceiveInspection")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromForm] ReceiveInspectionFormRequest request)
    {
        if (!_receiveInspectionRequestParser.TryParseFormRequest(request.Payload, request.Files, out var parsedRequest))
        {
            return BadRequest("Invalid payload JSON.");
        }

        var response = await _inspectionApplicationService.ReceiveInspectionAsync(parsedRequest, HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("{sessionId}")]
    [HttpGet("/QHVAC/GetInspection")]
    public async Task<ActionResult<GetInspectionResponse>> GetInspection(GetInspectionRequest request)
    {
        var response = await _inspectionApplicationService.GetInspectionAsync(request.SessionId!, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{sessionId}/files/{fileName}")]
    [HttpGet("/QHVAC/GetFile")]
    public async Task<IActionResult> GetFile(GetInspectionFileRequest request)
    {
        var response = await _inspectionApplicationService.GetFileAsync(
            request.SessionId!,
            request.FileName!,
            HttpContext.RequestAborted);
        return response is null
            ? NotFound()
            : File(response.Content, response.ContentType, response.FileName);
    }
}
