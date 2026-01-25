using System;
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
    private readonly IInspectionRequestHandler _inspectionRequestHandler;
    private readonly ILoginRequestHandler _loginRequestHandler;
    private readonly IRegisterRequestHandler _registerRequestHandler;

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        ILoginRequestHandler loginRequestHandler,
        IRegisterRequestHandler registerRequestHandler)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
        _loginRequestHandler = loginRequestHandler;
        _registerRequestHandler = registerRequestHandler;
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

        var response = await _inspectionRequestHandler.SaveRequestAsync(
            request,
            HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpPost(nameof(Login))]
    public ActionResult<LoginRequestResponse> Login(
        [FromBody] LoginRequestCommand request)
    {
        LoginRequestResponse response = _loginRequestHandler.HandleLogin(request);
        return Ok(response);
    }


    [HttpPost(nameof(Register))]
    public async Task<ActionResult<RegisterResponseModel>> Register(
        [FromBody] RegisterRequestModel request)
    {
        var userId = string.IsNullOrWhiteSpace(request.UserId)
            ? Guid.NewGuid().ToString("N")
            : request.UserId;
        userId = await _registerRequestHandler.HandleRegisterAsync(
            request,
            userId,
            HttpContext.RequestAborted);

        return Ok(new RegisterResponseModel(
            UserId: userId,
            FileCount: 0,
            UploadedBlobs: Array.Empty<string>()));
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
