using System.IO;
using System.Linq;
using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Handlers;

public sealed class InspectionRequestHandler
{
    private const string FilesContainerName = "Files";
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly QueueStorageOptions _queueOptions;
    private readonly TableStorageOptions _tableOptions;

    public InspectionRequestHandler(
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

    public async Task SaveRequestAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
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
        }

        await SaveFilesAsync(request, cancellationToken);
        await SaveInspectionFilesMetadataAsync(request, cancellationToken);

        await SendQueueMessageAsync(request, cancellationToken);
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

            var fileName = GetSafeFileName(file.FileName, i);

            var uploadBlobName = $"{sessionId}-{fileName}";
            var uploadBlobClient = filesContainerClient.GetBlobClient(uploadBlobName);

            await using var fileStream = file.OpenReadStream();
            await uploadBlobClient.UploadAsync(fileStream, overwrite: true, cancellationToken);
        }
    }

    private async Task SaveInspectionFilesMetadataAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (request.Files is not { Length: > 0 })
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_tableOptions.InspectionFilesTableName))
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
                FileName = GetSafeFileName(entry.file.FileName, entry.index),
                SessionID = sessionId,
                FileType = entry.file.ContentType ?? string.Empty
            })
            .ToArray();

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionFilesTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var entity = new InspectionFilesEntity
        {
            PartitionKey = InspectionFilesEntity.PartitionKeyValue,
            RowKey = sessionId,
            SessionId = sessionId,
            Files = JsonSerializer.Serialize(files)
        };

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    private static string GetSafeFileName(string? fileName, int index)
    {
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return $"file_{index}";
        }

        return safeFileName;
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
