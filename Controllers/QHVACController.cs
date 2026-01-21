using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly InspectionRequestHandler _inspectionRequestHandler;

    public QHVACController(
        InspectionRequestHandler inspectionRequestHandler)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
    }

    [HttpPost(nameof(ReceiveInspection))] //prod point
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromForm] string? payload,
        [FromForm] IFormFile[]? files)
    {
        if (!TryParseFormRequest(payload, files, out var request))
        {
            return BadRequest("Invalid payload JSON.");
        }

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

    private static bool TryParseFormRequest(
        string? payload,
        IFormFile[]? files,
        out ReceiveInspectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            request = new ReceiveInspectionRequest(
                SessionId: null,
                UserId: null,
                Name: null,
                QueryParams: null,
                Files: files);
            return true;
        }

        try
        {
            var normalizedPayload = NormalizePayload(payload);
            var parsed = JsonSerializer.Deserialize<ReceiveInspectionRequest>(
                normalizedPayload,
                JsonOptions);
            if (parsed is null)
            {
                request = new ReceiveInspectionRequest(
                    SessionId: null,
                    UserId: null,
                    Name: null,
                    QueryParams: null,
                    Files: files);
                return false;
            }

            request = parsed with { Files = files };
            return true;
        }
        catch (JsonException)
        {
            request = new ReceiveInspectionRequest(
                SessionId: null,
                UserId: null,
                Name: null,
                QueryParams: null,
                Files: files);
            return false;
        }
    }

    private static string NormalizePayload(string payload)
    {
        if (payload.Length >= 2 && payload[0] == '"' && payload[^1] == '"')
        {
            try
            {
                var unwrapped = JsonSerializer.Deserialize<string>(payload, JsonOptions);
                if (!string.IsNullOrWhiteSpace(unwrapped))
                {
                    return unwrapped;
                }
            }
            catch (JsonException)
            {
                return payload;
            }
        }

        return payload;
    }
}
