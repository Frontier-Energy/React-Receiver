using System.IO;
using System.Linq;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly QueueStorageOptions _queueOptions;

    public QHVACController(
        BlobServiceClient blobServiceClient,
        QueueServiceClient queueServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<QueueStorageOptions> queueOptions)
    {
        _blobServiceClient = blobServiceClient;
        _queueServiceClient = queueServiceClient;
        _blobOptions = blobOptions.Value;
        _queueOptions = queueOptions.Value;
    }

    [HttpPost(nameof(ReceiveInspection))] //prod point
    [Consumes("application/json")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromBody] ReceiveInspectionRequest request)
    {
        await SaveRequestAsync(request, HttpContext.RequestAborted);
        return Ok(BuildResponse(request));
    }

    [HttpPost(nameof(ReceiveInspection))]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspectionForm(
        [FromForm] ReceiveInspectionRequest request)
    {
        await SaveRequestAsync(request, HttpContext.RequestAborted);
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
        await SaveRequestAsync(request, HttpContext.RequestAborted);
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

    private async Task SaveRequestAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            return;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"{request.SessionId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var fileMetadata = request.Files?
            .Select(file => new { file.FileName, file.Length })
            .ToArray();

        var payload = new
        {
            request.SessionId,
            request.UserId,
            request.Name,
            request.QueryParams,
            Files = fileMetadata
        };

        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
        stream.Position = 0;

        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        if (request.Files is { Length: > 0 })
        {
            for (var i = 0; i < request.Files.Length; i++)
            {
                var file = request.Files[i];
                if (file is null || file.Length == 0)
                {
                    continue;
                }

                var extension = Path.GetExtension(file.FileName);
                var uploadBlobName = $"{request.SessionId}_upload_{i}{extension}";
                var uploadBlobClient = containerClient.GetBlobClient(uploadBlobName);

                await using var fileStream = file.OpenReadStream();
                await uploadBlobClient.UploadAsync(fileStream, overwrite: true, cancellationToken);
            }
        }

        await SendQueueMessageAsync(request, cancellationToken);
    }

    private async Task SendQueueMessageAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_queueOptions.QueueName))
        {
            return;
        }

        var queueClient = _queueServiceClient.GetQueueClient(_queueOptions.QueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            sessionId = request.SessionId ?? string.Empty
        });

        await queueClient.SendMessageAsync(payload, cancellationToken);
    }
}
