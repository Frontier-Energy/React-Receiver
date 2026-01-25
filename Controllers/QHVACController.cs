using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    private readonly IInspectionRequestHandler _inspectionRequestHandler;
    private readonly ILoginRequestHandler _loginRequestHandler;
    private readonly IReceiveInspectionRequestParser _receiveInspectionRequestParser;
    private readonly IRegisterRequestHandler _registerRequestHandler;

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        ILoginRequestHandler loginRequestHandler,
        IReceiveInspectionRequestParser receiveInspectionRequestParser,
        IRegisterRequestHandler registerRequestHandler)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
        _loginRequestHandler = loginRequestHandler;
        _receiveInspectionRequestParser = receiveInspectionRequestParser;
        _registerRequestHandler = registerRequestHandler;
    }

    [HttpPost(nameof(ReceiveInspection))] //prod point
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromForm] string? payload,
        [FromForm] IFormFile[]? files)
    {
        if (!_receiveInspectionRequestParser.TryParseFormRequest(payload, files, out var request))
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

}
