using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    private readonly InspectionRequestHandler _inspectionRequestHandler;

    public QHVACController(
        InspectionRequestHandler inspectionRequestHandler)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
    }

    [HttpPost(nameof(ReceiveInspection))] //prod point
    [Consumes("application/json")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromBody] ReceiveInspectionRequest request)
    {
        await _inspectionRequestHandler.SaveRequestAsync(request, HttpContext.RequestAborted);
        return Ok(BuildResponse(request));
    }

    [HttpPost(nameof(ReceiveInspection))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspectionForm(
        [FromForm] ReceiveInspectionRequest request)
    {
        await _inspectionRequestHandler.SaveRequestAsync(request, HttpContext.RequestAborted);
        return Ok(BuildResponse(request));
    }

    [HttpPost(nameof(Login))]
    public ActionResult<LoginRequestResponse> Login(
        [FromBody] LoginRequestCommand request)
    {
        var response = new LoginRequestResponse(UserId: Guid.NewGuid().ToString("N"));
        return Ok(response);
    }

    [HttpPost(nameof(Register))]
    public ActionResult<RegisterResponseModel> Register(
        [FromBody] RegisterRequestModel request)
    {
        var response = new RegisterResponseModel(UserId: Guid.NewGuid().ToString("N"));
        return Ok(response);
    }

    [HttpGet(nameof(ReceiveInspection))]   
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspectionGet(
        [FromQuery] ReceiveInspectionRequest request)
    {
        await _inspectionRequestHandler.SaveRequestAsync(request, HttpContext.RequestAborted);
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
