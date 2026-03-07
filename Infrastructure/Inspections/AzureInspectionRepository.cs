using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using React_Receiver.Domain.Inspections;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Inspections;

public sealed class AzureInspectionRepository : IInspectionRepository
{
    private const string FilesContainerName = "files";
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly QueueStorageOptions _queueOptions;
    private readonly TableStorageOptions _tableOptions;

    public AzureInspectionRepository(
        BlobServiceClient blobServiceClient,
        QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<QueueStorageOptions> queueOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _blobServiceClient = blobServiceClient;
        _queueServiceClient = queueServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions.Value;
        _queueOptions = queueOptions.Value;
        _tableOptions = tableOptions.Value;
    }

    public async Task<ReceiveInspectionResponse> SaveAsync(
        ReceiveInspectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient($"{request.SessionId}.json");
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
        }

        await SaveFilesAsync(request, cancellationToken);
        await SaveInspectionFilesMetadataAsync(request, cancellationToken);
        await SendQueueMessageAsync(request, cancellationToken);

        return new ReceiveInspectionResponse(
            "Received",
            request.SessionId ?? string.Empty,
            request.Name ?? string.Empty,
            request.QueryParams ?? new Dictionary<string, string>(),
            "OK");
    }

    public async Task<GetInspectionResponse?> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            return null;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobClient = containerClient.GetBlobClient($"{sessionId}.json");
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var payload = await LoadInspectionPayloadAsync(blobClient, cancellationToken);
        if (payload is null)
        {
            return null;
        }

        var files = await LoadInspectionFilesAsync(sessionId, cancellationToken);
        return new GetInspectionResponse(
            payload.SessionId ?? sessionId,
            payload.UserId,
            payload.Name,
            payload.QueryParams ?? new Dictionary<string, string>(),
            files);
    }

    public async Task<InspectionFileStreamResult?> GetFileAsync(
        string sessionId,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            return null;
        }

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return null;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(FilesContainerName);
        var blobClient = containerClient.GetBlobClient($"{sessionId}-{safeFileName}");
        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new InspectionFileStreamResult(
            download.Value.Content,
            download.Value.Details.ContentType ?? "application/octet-stream",
            safeFileName);
    }

    private async Task SaveFilesAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (request.Files is not { Length: > 0 })
        {
            return;
        }

        var sessionId = request.SessionId ?? string.Empty;
        var filesContainerClient = _blobServiceClient.GetBlobContainerClient(FilesContainerName);
        await filesContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        for (var i = 0; i < request.Files.Length; i++)
        {
            var file = request.Files[i];
            if (file is null || file.Length == 0)
            {
                continue;
            }

            var fileName = InspectionFileName.Sanitize(file.FileName, i);
            var blobClient = filesContainerClient.GetBlobClient($"{sessionId}-{fileName}");
            await using var fileStream = file.OpenReadStream();
            await blobClient.UploadAsync(fileStream, overwrite: true, cancellationToken);
        }
    }

    private async Task SaveInspectionFilesMetadataAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (request.Files is not { Length: > 0 } ||
            string.IsNullOrWhiteSpace(_tableOptions.InspectionFilesTableName))
        {
            return;
        }

        var sessionId = request.SessionId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var files = request.Files
            .Select((file, index) => new { file, index })
            .Where(entry => entry.file is not null && entry.file.Length > 0)
            .Select(entry => new
            {
                FileName = InspectionFileName.Sanitize(entry.file.FileName, entry.index),
                SessionID = sessionId,
                FileType = entry.file.ContentType ?? string.Empty
            })
            .ToArray();

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionFilesTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await tableClient.UpsertEntityAsync(
            new InspectionFilesEntity
            {
                PartitionKey = InspectionFilesEntity.PartitionKeyValue,
                RowKey = sessionId,
                SessionId = sessionId,
                Files = JsonSerializer.Serialize(files)
            },
            TableUpdateMode.Replace,
            cancellationToken);
    }

    private async Task SendQueueMessageAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_queueOptions.QueueName))
        {
            return;
        }

        var queueClient = _queueServiceClient.GetQueueClient(_queueOptions.QueueName);
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await queueClient.SendMessageAsync(
            JsonSerializer.Serialize(new { sessionId = request.SessionId ?? string.Empty }),
            cancellationToken);
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

            return string.IsNullOrWhiteSpace(response.Value.Files)
                ? Array.Empty<InspectionFileReference>()
                : JsonSerializer.Deserialize<InspectionFileReference[]>(
                    response.Value.Files,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<InspectionFileReference>();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Array.Empty<InspectionFileReference>();
        }
    }

    private sealed class InspectionPayload
    {
        public string? SessionId { get; set; }
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
    }
}
