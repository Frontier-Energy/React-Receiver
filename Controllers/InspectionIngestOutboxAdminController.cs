using MediatR;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("inspections/admin/outbox")]
public sealed class InspectionIngestOutboxAdminController : ControllerBase
{
    private readonly ISender _sender;

    public InspectionIngestOutboxAdminController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public Task<IReadOnlyCollection<InspectionIngestOutboxSessionSummary>> GetSessions([FromQuery] GetInspectionIngestOutboxRequest request)
    {
        var limit = request.Limit <= 0 ? 50 : Math.Min(request.Limit, 200);
        return _sender.Send(new GetInspectionIngestOutboxQuery(request.Status, limit), HttpContext.RequestAborted);
    }

    [HttpGet("{sessionId}")]
    public async Task<ActionResult<InspectionIngestOutboxSessionDetail>> GetSession([FromRoute] string sessionId)
    {
        var response = await _sender.Send(
            new GetInspectionIngestOutboxSessionQuery(sessionId),
            HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{sessionId}/replay")]
    public async Task<ActionResult<ReplayInspectionIngestSessionResponse>> ReplaySession(
        [FromRoute] string sessionId,
        [FromBody] ReplayInspectionIngestOutboxRequest? request)
    {
        var response = await _sender.Send(
            new ReplayInspectionIngestOutboxSessionCommand(sessionId, request?.Force ?? false),
            HttpContext.RequestAborted);

        if (response.Session is null && !response.Accepted)
        {
            return NotFound(response);
        }

        return response.Accepted ? Ok(response) : Conflict(response);
    }
}
