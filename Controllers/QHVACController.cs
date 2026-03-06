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
    private readonly ITenantConfigHandler _tenantConfigHandler;
    private readonly IInspectionQueryService _inspectionQueryService;
    private readonly IUserQueryService _userQueryService;
    private readonly IFormSchemaService _formSchemaService;
    private readonly ITranslationService _translationService;

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        ILoginRequestHandler loginRequestHandler,
        IReceiveInspectionRequestParser receiveInspectionRequestParser,
        IRegisterRequestHandler registerRequestHandler,
        ITenantConfigHandler tenantConfigHandler,
        Azure.Storage.Blobs.BlobServiceClient blobServiceClient,
        Azure.Data.Tables.TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
        : this(
            inspectionRequestHandler,
            loginRequestHandler,
            receiveInspectionRequestParser,
            registerRequestHandler,
            tenantConfigHandler,
            new InspectionQueryService(blobServiceClient, tableServiceClient, blobOptions, tableOptions),
            new UserQueryService(tableServiceClient, tableOptions),
            new FormSchemaService(tableServiceClient, tableOptions),
            new TranslationService(tableServiceClient, tableOptions))
    {
    }

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        ILoginRequestHandler loginRequestHandler,
        IReceiveInspectionRequestParser receiveInspectionRequestParser,
        IRegisterRequestHandler registerRequestHandler,
        ITenantConfigHandler tenantConfigHandler,
        IInspectionQueryService inspectionQueryService,
        IUserQueryService userQueryService,
        IFormSchemaService formSchemaService,
        ITranslationService translationService)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
        _loginRequestHandler = loginRequestHandler;
        _receiveInspectionRequestParser = receiveInspectionRequestParser;
        _registerRequestHandler = registerRequestHandler;
        _tenantConfigHandler = tenantConfigHandler;
        _inspectionQueryService = inspectionQueryService;
        _userQueryService = userQueryService;
        _formSchemaService = formSchemaService;
        _translationService = translationService;
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

    [HttpGet("form-schemas")]
    public async Task<ActionResult<FormSchemaCatalogResponse>> ListFormSchemas()
    {
        var response = await _formSchemaService.ListAsync(HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("form-schemas/{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> GetFormSchema([FromRoute] string formType)
    {
        if (string.IsNullOrWhiteSpace(formType))
        {
            return BadRequest("formType is required.");
        }

        var response = await _formSchemaService.GetAsync(formType, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("form-schemas/{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> UpsertFormSchema(
        [FromRoute] string formType,
        [FromBody] FormSchemaResponse request)
    {
        if (string.IsNullOrWhiteSpace(formType))
        {
            return BadRequest("formType is required.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.FormName) || request.Sections is null)
        {
            return BadRequest("A valid form schema payload is required.");
        }

        var response = await _formSchemaService.UpsertAsync(formType, request, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("translations/{language}")]
    public async Task<ActionResult<TranslationsResponse>> GetTranslations([FromRoute] string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return BadRequest("language is required.");
        }

        var response = await _translationService.GetAsync(language, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("translations/{language}")]
    public async Task<ActionResult<TranslationsResponse>> UpsertTranslations(
        [FromRoute] string language,
        [FromBody] TranslationsResponse request)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return BadRequest("language is required.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.LanguageName) || request.App is null)
        {
            return BadRequest("A valid translations payload is required.");
        }

        var response = await _translationService.UpsertAsync(language, request, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("tenant-config")]
    public async Task<ActionResult<TenantBootstrapResponse>> GetTenantConfig([FromQuery] string? tenantId)
    {
        var tenantConfig = await _tenantConfigHandler.GetTenantConfigAsync(tenantId, HttpContext.RequestAborted);
        return tenantConfig is null ? NotFound() : Ok(tenantConfig);
    }

    [HttpPost("tenant-config")]
    public async Task<ActionResult<TenantBootstrapResponse>> UpsertTenantConfig([FromBody] TenantBootstrapResponse request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            request.UiDefaults is null ||
            string.IsNullOrWhiteSpace(request.UiDefaults.Theme) ||
            string.IsNullOrWhiteSpace(request.UiDefaults.Font) ||
            string.IsNullOrWhiteSpace(request.UiDefaults.Language) ||
            request.EnabledForms is null)
        {
            return BadRequest("A valid tenant bootstrap payload is required.");
        }

        var tenantConfig = await _tenantConfigHandler.UpsertTenantConfigAsync(request, HttpContext.RequestAborted);
        return Ok(tenantConfig);
    }
}
