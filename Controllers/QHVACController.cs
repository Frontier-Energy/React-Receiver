using System;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IInspectionRequestHandler _inspectionRequestHandler;
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public QHVACController(
        IInspectionRequestHandler inspectionRequestHandler,
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _inspectionRequestHandler = inspectionRequestHandler;
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
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
        LoginRequestResponse response = HandleLogin(request);
        return Ok(response);
    }

    private static LoginRequestResponse HandleLogin( LoginRequestCommand request)
    {
        return new LoginRequestResponse(UserId: Guid.NewGuid().ToString("N"));
    }


    [HttpPost(nameof(Register))]
    public async Task<ActionResult<RegisterResponseModel>> Register(
        [FromBody] RegisterRequestModel request)
    {
        var userId = string.IsNullOrWhiteSpace(request.UserId)
            ? Guid.NewGuid().ToString("N")
            : request.UserId;
        userId = await HandleRegister(request, userId);

        return Ok(new RegisterResponseModel(
            UserId: userId,
            FileCount: 0,
            UploadedBlobs: Array.Empty<string>()));
    }

    private async Task<string> HandleRegister(RegisterRequestModel request, string userId)
    {
        if (!string.IsNullOrWhiteSpace(_tableOptions.ConnectionString))
        {
            var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);
            await tableClient.CreateIfNotExistsAsync(cancellationToken: HttpContext.RequestAborted);

            UserEntity? existing = null;
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var filter = TableClient.CreateQueryFilter<UserEntity>(entity =>
                    entity.PartitionKey == UserEntity.PartitionKeyValue &&
                    entity.Email == request.Email);
                await foreach (var entity in tableClient.QueryAsync<UserEntity>(
                    filter: filter,
                    cancellationToken: HttpContext.RequestAborted))
                {
                    existing = entity;
                    break;
                }
            }
            else
            {
                try
                {
                    var response = await tableClient.GetEntityAsync<UserEntity>(
                        UserEntity.PartitionKeyValue,
                        userId,
                        cancellationToken: HttpContext.RequestAborted);
                    existing = response.Value;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    existing = null;
                }
            }

            if (existing is null)
            {
                var entity = new UserEntity
                {
                    PartitionKey = UserEntity.PartitionKeyValue,
                    RowKey = userId,
                    UserId = userId,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName
                };

                await tableClient.AddEntityAsync(entity, HttpContext.RequestAborted);
            }
            else
            {
                userId = existing.UserId;
            }
        }

        return userId;
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
