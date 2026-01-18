using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("[controller]")]
public sealed class QHVACController : ControllerBase
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _blobOptions;

    public QHVACController(
        BlobServiceClient blobServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions)
    {
        _blobServiceClient = blobServiceClient;
        _blobOptions = blobOptions.Value;
    }

    [HttpPost(nameof(ReceiveInspection))] //prod point
    public async Task<ActionResult<ReceiveInspectionResponse>> ReceiveInspection(
        [FromBody] ReceiveInspectionRequest request)
    {
        await SaveRequestAsync(request, HttpContext.RequestAborted);
        return Ok(BuildResponse(request));
    }

    [HttpGet(nameof(ReceiveInspection))]   //Testing only - http://localhost:5108/QHVAC/ReceiveInspection?SessionId=abc123&Name=Test&QueryParams[foo]=bar&QueryParams[priority]=high
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

        var blobName = $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, request, cancellationToken: cancellationToken);
        stream.Position = 0;

        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
    }
}
