using System;
using System.IO;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly TableStorageOptions _tableOptions;

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        ILoginRequestHandler loginRequestHandler,
        IReceiveInspectionRequestParser receiveInspectionRequestParser,
        IRegisterRequestHandler registerRequestHandler,
        ITenantConfigHandler tenantConfigHandler,
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient,
        IOptions<BlobStorageOptions> blobOptions,
        IOptions<TableStorageOptions> tableOptions)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
        _loginRequestHandler = loginRequestHandler;
        _receiveInspectionRequestParser = receiveInspectionRequestParser;
        _registerRequestHandler = registerRequestHandler;
        _tenantConfigHandler = tenantConfigHandler;
        _blobServiceClient = blobServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions.Value;
        _tableOptions = tableOptions.Value;
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

        if (string.IsNullOrWhiteSpace(_tableOptions.TableName))
        {
            return NotFound();
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<UserEntity>(
                UserEntity.PartitionKeyValue,
                request.UserId,
                cancellationToken: HttpContext.RequestAborted);

            var entity = response.Value;
            var user = new UserModel(
                UserId: entity.UserId,
                Email: entity.Email,
                FirstName: entity.FirstName,
                LastName: entity.LastName);

            return Ok(new GetUserResponse(User: user));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return NotFound();
        }
    }

    [HttpGet(nameof(GetInspection))]
    public async Task<ActionResult<GetInspectionResponse>> GetInspection([FromQuery] string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("SessionId is required.");
        }

        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            return NotFound();
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobClient = containerClient.GetBlobClient($"{sessionId}.json");

        if (!await blobClient.ExistsAsync(HttpContext.RequestAborted))
        {
            return NotFound();
        }

        var payload = await LoadInspectionPayloadAsync(blobClient, HttpContext.RequestAborted);
        if (payload is null)
        {
            return NotFound();
        }

        var files = await LoadInspectionFilesAsync(sessionId, HttpContext.RequestAborted);

        var response = new GetInspectionResponse(
            SessionId: payload.SessionId ?? sessionId,
            UserId: payload.UserId,
            Name: payload.Name,
            QueryParams: payload.QueryParams ?? new Dictionary<string, string>(),
            Files: files);

        return Ok(response);
    }

    [HttpGet(nameof(GetFile))]
    public async Task<IActionResult> GetFile([FromQuery] string sessionId, [FromQuery] string fileName)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("SessionId and fileName are required.");
        }

        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            return NotFound();
        }

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return BadRequest("fileName is invalid.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient("files");
        var blobClient = containerClient.GetBlobClient($"{sessionId}-{safeFileName}");

        if (!await blobClient.ExistsAsync(HttpContext.RequestAborted))
        {
            return NotFound();
        }

        var download = await blobClient.DownloadStreamingAsync(cancellationToken: HttpContext.RequestAborted);
        var contentType = download.Value.Details.ContentType ?? "application/octet-stream";
        return File(download.Value.Content, contentType, safeFileName);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> GetCurrentUser()
    {
        var defaultResponse = new MeResponse(
            UserId: "a1b2c3",
            Roles: ["admin"],
            Permissions: ["tenant.select", "customization.admin"]);

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.MeTableName))
        {
            return Ok(defaultResponse);
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.MeTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: HttpContext.RequestAborted);

        try
        {
            var response = await tableClient.GetEntityAsync<MeEntity>(
                MeEntity.PartitionKeyValue,
                MeEntity.CurrentUserRowKey,
                cancellationToken: HttpContext.RequestAborted);
            return Ok(response.Value.ToResponse());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            await tableClient.UpsertEntityAsync(
                MeEntity.FromResponse(defaultResponse),
                TableUpdateMode.Replace,
                HttpContext.RequestAborted);
            return Ok(defaultResponse);
        }
    }

    [HttpGet("form-schemas")]
    public async Task<ActionResult<FormSchemaCatalogResponse>> ListFormSchemas()
    {
        var defaultItems = FormSchemaCatalog
            .Select(item => item.Value.ToCatalogItem(item.Key))
            .ToArray();

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.FormSchemaCatalogTableName))
        {
            return Ok(new FormSchemaCatalogResponse(Items: defaultItems));
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemaCatalogTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: HttpContext.RequestAborted);

        var items = new List<FormSchemaCatalogItemResponse>();
        await foreach (var entity in tableClient.QueryAsync<FormSchemaCatalogItemEntity>(
                           e => e.PartitionKey == FormSchemaCatalogItemEntity.PartitionKeyValue,
                           cancellationToken: HttpContext.RequestAborted))
        {
            items.Add(entity.ToResponse());
        }

        if (items.Count == 0)
        {
            foreach (var defaultItem in defaultItems)
            {
                await tableClient.UpsertEntityAsync(
                    FormSchemaCatalogItemEntity.FromResponse(defaultItem),
                    TableUpdateMode.Replace,
                    HttpContext.RequestAborted);
            }

            items.AddRange(defaultItems);
        }

        return Ok(new FormSchemaCatalogResponse(Items: items.ToArray()));
    }

    [HttpGet("form-schemas/{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> GetFormSchema([FromRoute] string formType)
    {
        if (string.IsNullOrWhiteSpace(formType))
        {
            return BadRequest("formType is required.");
        }

        if (!FormSchemaCatalog.TryGetValue(formType, out var defaultSchema))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.FormSchemasTableName))
        {
            return Ok(defaultSchema.Schema);
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: HttpContext.RequestAborted);

        try
        {
            var response = await tableClient.GetEntityAsync<FormSchemaEntity>(
                FormSchemaEntity.PartitionKeyValue,
                formType,
                cancellationToken: HttpContext.RequestAborted);
            return Ok(response.Value.ToResponse());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            await tableClient.UpsertEntityAsync(
                FormSchemaEntity.FromResponse(formType, defaultSchema.Schema),
                TableUpdateMode.Replace,
                HttpContext.RequestAborted);
            return Ok(defaultSchema.Schema);
        }
    }

    [HttpGet("translations/{language}")]
    public async Task<ActionResult<TranslationsResponse>> GetTranslations([FromRoute] string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return BadRequest("language is required.");
        }

        if (!Translations.TryGetValue(language, out var defaultTranslations))
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.TranslationsTableName))
        {
            return Ok(defaultTranslations);
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: HttpContext.RequestAborted);

        try
        {
            var response = await tableClient.GetEntityAsync<TranslationEntity>(
                TranslationEntity.PartitionKeyValue,
                language,
                cancellationToken: HttpContext.RequestAborted);
            return Ok(response.Value.ToResponse());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            await tableClient.UpsertEntityAsync(
                TranslationEntity.FromResponse(language, defaultTranslations),
                TableUpdateMode.Replace,
                HttpContext.RequestAborted);
            return Ok(defaultTranslations);
        }
    }

    [HttpGet("tenant-config")]
    public async Task<ActionResult<TenantBootstrapResponse>> GetTenantConfig()
    {
        var tenantConfig = await _tenantConfigHandler.GetTenantConfigAsync(HttpContext.RequestAborted);
        return Ok(tenantConfig);
    }

    [HttpPost("tenant-config")]
    public async Task<ActionResult<TenantBootstrapResponse>> UpsertTenantConfig([FromBody] TenantBootstrapResponse request)
    {
        var tenantConfig = await _tenantConfigHandler.UpsertTenantConfigAsync(request, HttpContext.RequestAborted);
        return Ok(tenantConfig);
    }

    private static async Task<InspectionPayload?> LoadInspectionPayloadAsync(
        BlobClient blobClient,
        CancellationToken cancellationToken)
    {
        var download = await blobClient.DownloadContentAsync(cancellationToken);
        if (download.Value.Content.ToMemory().Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<InspectionPayload>(
            download.Value.Content.ToString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private async Task<InspectionFileReference[]> LoadInspectionFilesAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.InspectionFilesTableName))
        {
            return Array.Empty<InspectionFileReference>();
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionFilesTableName);

        try
        {
            var response = await tableClient.GetEntityAsync<InspectionFilesEntity>(
                InspectionFilesEntity.PartitionKeyValue,
                sessionId,
                cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(response.Value.Files))
            {
                return Array.Empty<InspectionFileReference>();
            }

            var files = JsonSerializer.Deserialize<InspectionFileReference[]>(
                response.Value.Files,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return files ?? Array.Empty<InspectionFileReference>();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Array.Empty<InspectionFileReference>();
        }
    }

    private sealed record FormSchemaCatalogEntry(
        string Version,
        string Etag,
        FormSchemaResponse Schema
    )
    {
        public FormSchemaCatalogItemResponse ToCatalogItem(string formType) =>
            new(
                FormType: formType,
                Version: Version,
                Etag: Etag);
    }

    private static readonly Dictionary<string, FormSchemaCatalogEntry> FormSchemaCatalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["hvac"] = new FormSchemaCatalogEntry(
                Version: "2026-03-05",
                Etag: "\"hvac-v1\"",
                Schema: new FormSchemaResponse(
                    FormName: "HVAC Inspection",
                    Sections:
                    [
                        new FormSectionResponse(
                            Title: "Equipment Information",
                            Fields:
                            [
                                new FormFieldResponse(
                                    Id: "unitLocation",
                                    Label: "Unit Location",
                                    Type: "text",
                                    Required: true,
                                    ExternalID: "hvac.unitLocation",
                                    Placeholder: "e.g., Attic, Basement, Roof",
                                    ValidationRules:
                                    [
                                        new ValidationRuleResponse(
                                            Type: "minLength",
                                            Value: 3,
                                            Message: "Unit location must be at least 3 characters")
                                    ])
                            ])
                    ])),
            ["electrical"] = new FormSchemaCatalogEntry(
                Version: "2026-03-05",
                Etag: "\"electrical-v1\"",
                Schema: new FormSchemaResponse(
                    FormName: "Electrical Inspection",
                    Sections:
                    [
                        new FormSectionResponse(
                            Title: "General",
                            Fields:
                            [
                                new FormFieldResponse(
                                    Id: "panelCondition",
                                    Label: "Panel Condition",
                                    Type: "select",
                                    Required: true,
                                    ExternalID: "electrical.panelCondition",
                                    Options:
                                    [
                                        new FormFieldOptionResponse("Good", "good"),
                                        new FormFieldOptionResponse("Needs Attention", "needs-attention")
                                    ])
                            ])
                    ])),
            ["electrical-sf"] = new FormSchemaCatalogEntry(
                Version: "2026-03-05",
                Etag: "\"electrical-sf-v1\"",
                Schema: new FormSchemaResponse(
                    FormName: "Electrical SF Inspection",
                    Sections:
                    [
                        new FormSectionResponse(
                            Title: "Service",
                            Fields:
                            [
                                new FormFieldResponse(
                                    Id: "serviceAmps",
                                    Label: "Service Amperage",
                                    Type: "number",
                                    Required: true,
                                    ExternalID: "electrical.serviceAmps")
                            ])
                    ])),
            ["safety-checklist"] = new FormSchemaCatalogEntry(
                Version: "2026-03-05",
                Etag: "\"safety-checklist-v1\"",
                Schema: new FormSchemaResponse(
                    FormName: "Safety Checklist",
                    Sections:
                    [
                        new FormSectionResponse(
                            Title: "Site Safety",
                            Fields:
                            [
                                new FormFieldResponse(
                                    Id: "ppeVerified",
                                    Label: "PPE Verified",
                                    Type: "checkbox",
                                    Required: true,
                                    ExternalID: "safety.ppeVerified")
                            ])
                    ]))
        };

    private static readonly Dictionary<string, TranslationsResponse> Translations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new TranslationsResponse(
                LanguageName: "English",
                App: new TranslationAppResponse(
                    Title: "Data Intake Tool",
                    PoweredBy: "Powered By",
                    Brand: "QControl")),
            ["es"] = new TranslationsResponse(
                LanguageName: "Espanol",
                App: new TranslationAppResponse(
                    Title: "Herramienta de Captura de Datos",
                    PoweredBy: "Desarrollado por",
                    Brand: "QControl"))
        };

    private sealed class InspectionPayload
    {
        public string? SessionId { get; set; }
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
    }
}
