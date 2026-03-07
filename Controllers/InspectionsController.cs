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

    public InspectionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromForm] ReceiveInspectionFormRequest request)
    {
        var response = await _sender.Send(new ReceiveInspectionCommand(request), HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<GetInspectionResponse>> GetInspection(GetInspectionRequest request)
    {
        var response = await _sender.Send(new GetInspectionQuery(request.SessionId!), HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{sessionId}/files/{fileName}")]
    [HttpGet("/QHVAC/GetFile")]
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
