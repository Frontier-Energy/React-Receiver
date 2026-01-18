using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    [HttpPost(nameof(ReceiveInspection))] //prod point
    public ActionResult<ReceiveInspectionResponse> ReceiveInspection(
        [FromBody] ReceiveInspectionRequest request)
    {
        return Ok(BuildResponse(request));
    }

    [HttpGet(nameof(ReceiveInspection))]   //Testing only - http://localhost:5108/QHVAC/ReceiveInspection?SessionId=abc123&Name=Test&QueryParams[foo]=bar&QueryParams[priority]=high
    public ActionResult<ReceiveInspectionResponse> ReceiveInspectionGet(
        [FromQuery] ReceiveInspectionRequest request)
    {
        return Ok(BuildResponse(request));
    }

    private static ReceiveInspectionResponse BuildResponse(ReceiveInspectionRequest request)
    {
        var queryParams = request.QueryParams ?? new Dictionary<string, string>();

        return new ReceiveInspectionResponse(
            Status: "Received",
            SessionId: request.SessionId ?? string.Empty,
            Name: request.Name ?? string.Empty,
            QueryParams: queryParams,
            Message: "OK");
    }
}
