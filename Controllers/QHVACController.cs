using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    private readonly IInspectionRequestHandler _inspectionRequestHandler;
    private readonly ILoginRequestHandler _loginRequestHandler;
    private readonly IReceiveInspectionRequestParser _receiveInspectionRequestParser;
    private readonly IRegisterRequestHandler _registerRequestHandler;
    private readonly IInspectionQueryService _inspectionQueryService;
    private readonly IUserQueryService _userQueryService;

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        ILoginRequestHandler loginRequestHandler,
        IReceiveInspectionRequestParser receiveInspectionRequestParser,
        IRegisterRequestHandler registerRequestHandler,
        Azure.Storage.Blobs.BlobServiceClient blobServiceClient,
        Azure.Data.Tables.TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
        : this(
            inspectionRequestHandler,
            loginRequestHandler,
            receiveInspectionRequestParser,
            registerRequestHandler,
            new InspectionQueryService(blobServiceClient, tableServiceClient, blobOptions, tableOptions),
            new UserQueryService(tableServiceClient, tableOptions))
    {
    }

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        ILoginRequestHandler loginRequestHandler,
        IReceiveInspectionRequestParser receiveInspectionRequestParser,
        IRegisterRequestHandler registerRequestHandler,
        IInspectionQueryService inspectionQueryService,
        IUserQueryService userQueryService)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
        _loginRequestHandler = loginRequestHandler;
        _receiveInspectionRequestParser = receiveInspectionRequestParser;
        _registerRequestHandler = registerRequestHandler;
        _inspectionQueryService = inspectionQueryService;
        _userQueryService = userQueryService;
    }

    [HttpPost(nameof(ReceiveInspection))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromForm] string? payload,
        [FromForm] IFormFile[]? files)
    {
        if (!_receiveInspectionRequestParser.TryParseFormRequest(payload, files, out var request))
        {
            return BadRequest("Invalid payload JSON.");
        }

        var response = await _inspectionRequestHandler.SaveRequestAsync(request, HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpPost(nameof(Login))]
    public ActionResult<LoginRequestResponse> Login([FromBody] LoginRequestCommand request)
    {
        var response = _loginRequestHandler.HandleLogin(request);
        return Ok(response);
    }

    [HttpPost(nameof(Register))]
    public async Task<ActionResult<RegisterResponseModel>> Register([FromBody] RegisterRequestModel request)
    {
        var userId = Guid.NewGuid().ToString("N");
        userId = await _registerRequestHandler.HandleRegisterAsync(
            request,
            userId,
            HttpContext.RequestAborted);

        return Ok(new RegisterResponseModel(UserId: userId));
    }

    [HttpPost(nameof(GetUser))]
    public async Task<ActionResult<GetUserResponse>> GetUser([FromBody] GetUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest("UserId is required.");
        }

        var response = await _userQueryService.GetUserAsync(request.UserId, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet(nameof(GetInspection))]
    public async Task<ActionResult<GetInspectionResponse>> GetInspection([FromQuery] string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("SessionId is required.");
        }

        var response = await _inspectionQueryService.GetInspectionAsync(sessionId, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet(nameof(GetFile))]
    public async Task<IActionResult> GetFile([FromQuery] string sessionId, [FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("SessionId and fileName are required.");
        }

        var response = await _inspectionQueryService.GetFileAsync(sessionId, fileName, HttpContext.RequestAborted);
        return response is null
            ? NotFound()
            : File(response.Content, response.ContentType, response.FileName);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> GetCurrentUser()
    {
        var response = await _userQueryService.GetCurrentUserAsync(HttpContext.RequestAborted);
        return Ok(response);
    }
}
