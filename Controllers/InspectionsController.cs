using MediatR;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("inspections")]
public sealed class InspectionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<InspectionsController> _logger;

    public InspectionsController(ISender sender, ILogger<InspectionsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = ReceiveInspectionFormRequest.MaxMultipartBodyLengthBytes)]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromForm] ReceiveInspectionFormRequest request)
    {
        _logger.LogInformation(
            "Processing inspection ingestion request with {FileCount} uploaded files",
            request.Files?.Length ?? 0);
        var response = await _sender.Send(new ReceiveInspectionCommand(request), HttpContext.RequestAborted);
        _logger.LogInformation(
            "Completed inspection ingestion for {SessionId} with status {Status}",
            response.SessionId,
            response.Status);
        return Ok(response);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<GetInspectionResponse>> GetInspection(GetInspectionRequest request)
    {
        var response = await _sender.Send(new GetInspectionQuery(request.SessionId!), HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{sessionId}/files/{fileName}")]
    public async Task<IActionResult> GetFile(GetInspectionFileRequest request)
    {
        var response = await _sender.Send(
            new GetInspectionFileQuery(request.SessionId!, request.FileName!),
            HttpContext.RequestAborted);
        return response is null
            ? NotFound()
            : File(response.Content, response.ContentType, response.FileName);
    }
}
