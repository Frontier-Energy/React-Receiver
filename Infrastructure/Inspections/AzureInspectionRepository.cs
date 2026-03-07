using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using React_Receiver.Domain.Inspections;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Inspections;

public sealed class AzureInspectionRepository : IInspectionRepository
{
    private const string FilesContainerName = "files";
    private static readonly TimeSpan ProcessingLeaseDuration = TimeSpan.FromMinutes(2);
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly QueueStorageOptions _queueOptions;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;

    public AzureInspectionRepository(
        BlobServiceClient blobServiceClient,
        QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<QueueStorageOptions> queueOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions,
        IStorageOperationObserver storageObserver)
    {
        _blobServiceClient = blobServiceClient;
        _queueServiceClient = queueServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions.Value;
        _queueOptions = queueOptions.Value;
        _tableOptions = tableOptions.Value;
        _storageObserver = storageObserver;
    }

    public async Task<ReceiveInspectionResponse> PrepareAsync(
        ReceiveInspectionRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = NormalizeRequest(request);
        var manifest = BuildManifest(normalizedRequest);
        var outboxEntity = await CreateOrLoadOutboxEntityAsync(normalizedRequest, manifest, cancellationToken);

        if (outboxEntity.Completed || (outboxEntity.PayloadStaged && outboxEntity.FilesStaged))
        {
            return BuildResponse(normalizedRequest);
        }

        try
        {
            if (!outboxEntity.PayloadStaged)
            {
                await SavePayloadAsync(normalizedRequest, cancellationToken);
                outboxEntity.PayloadStaged = true;
                outboxEntity.Status = "PayloadStaged";
                outboxEntity.LastError = string.Empty;
                outboxEntity.NextAttemptAtUtc = DateTimeOffset.UtcNow;
                await SaveOutboxEntityAsync(outboxEntity, cancellationToken);
            }

            if (!outboxEntity.FilesStaged)
            {
                await SaveFilesAsync(normalizedRequest, manifest, cancellationToken);
                outboxEntity.FilesStaged = true;
                outboxEntity.Status = "Staged";
                outboxEntity.LastError = string.Empty;
                outboxEntity.NextAttemptAtUtc = DateTimeOffset.UtcNow;
                await SaveOutboxEntityAsync(outboxEntity, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            await CompensateStagingAsync(normalizedRequest.SessionId ?? string.Empty, manifest, cancellationToken);

            outboxEntity.PayloadStaged = false;
            outboxEntity.FilesStaged = false;
            outboxEntity.MetadataWritten = false;
            outboxEntity.QueueMessageSent = false;
            outboxEntity.Completed = false;
            outboxEntity.Processing = false;
            outboxEntity.LockedUntilUtc = null;
            outboxEntity.Status = "Compensated";
            outboxEntity.LastError = Truncate(ex.Message, 2048);
            outboxEntity.NextAttemptAtUtc = null;
            await SaveOutboxEntityAsync(outboxEntity, cancellationToken);
            throw;
        }

        return BuildResponse(normalizedRequest);
    }

    public async Task<bool> ProcessPendingAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var outboxEntity = await TryAcquireLeaseAsync(sessionId, cancellationToken);
        if (outboxEntity is null)
        {
            return false;
        }

        try
        {
            if (!outboxEntity.MetadataWritten)
            {
                await SaveInspectionFilesMetadataAsync(outboxEntity, cancellationToken);
                outboxEntity.MetadataWritten = true;
                outboxEntity.Status = "MetadataWritten";
                outboxEntity.LastError = string.Empty;
                outboxEntity.NextAttemptAtUtc = DateTimeOffset.UtcNow;
                await SaveOutboxEntityAsync(outboxEntity, cancellationToken);
            }

            if (!outboxEntity.QueueMessageSent)
            {
                await SendQueueMessageAsync(outboxEntity.SessionId, cancellationToken);
                outboxEntity.QueueMessageSent = true;
            }

            outboxEntity.Completed = true;
            outboxEntity.Processing = false;
            outboxEntity.LockedUntilUtc = null;
            outboxEntity.Status = "Completed";
            outboxEntity.LastError = string.Empty;
            outboxEntity.NextAttemptAtUtc = null;
            await SaveOutboxEntityAsync(outboxEntity, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            outboxEntity.Completed = false;
            outboxEntity.Processing = false;
            outboxEntity.LockedUntilUtc = null;
            outboxEntity.RetryCount++;
            outboxEntity.Status = "PendingRetry";
            outboxEntity.LastError = Truncate(ex.Message, 2048);
            outboxEntity.NextAttemptAtUtc = DateTimeOffset.UtcNow.Add(GetRetryDelay(outboxEntity.RetryCount));
            await SaveOutboxEntityAsync(outboxEntity, cancellationToken);
            return false;
        }
    }

    public async Task<IReadOnlyCollection<string>> GetPendingSessionIdsAsync(
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (maxResults <= 0)
        {
            return Array.Empty<string>();
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var now = DateTimeOffset.UtcNow;
        var sessions = new List<string>(maxResults);

        await foreach (var entity in tableClient.QueryAsync<InspectionIngestOutboxEntity>(
                           entity => entity.PartitionKey == InspectionIngestOutboxEntity.PartitionKeyValue,
                           maxPerPage: maxResults,
                           cancellationToken: cancellationToken))
        {
            if (entity.Completed ||
                !entity.PayloadStaged ||
                !entity.FilesStaged ||
                entity.Processing ||
                (entity.LockedUntilUtc is not null && entity.LockedUntilUtc > now) ||
                (entity.NextAttemptAtUtc is not null && entity.NextAttemptAtUtc > now))
            {
                continue;
            }

            sessions.Add(entity.SessionId);
            if (sessions.Count >= maxResults)
            {
                break;
            }
        }

        return sessions;
    }

    public async Task<GetInspectionResponse?> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            return null;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobClient = containerClient.GetBlobClient($"{sessionId}.json");
        if (!await _storageObserver.ExecuteAsync(
                "blob",
                _blobOptions.ContainerName,
                "InspectionPayloadExists",
                ct => blobClient.ExistsAsync(ct),
                cancellationToken))
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
        if (!await _storageObserver.ExecuteAsync(
                "blob",
                FilesContainerName,
                "InspectionFileExists",
                ct => blobClient.ExistsAsync(ct),
                cancellationToken))
        {
            return null;
        }

        var download = await _storageObserver.ExecuteAsync(
            "blob",
            FilesContainerName,
            "DownloadInspectionFile",
            ct => blobClient.DownloadStreamingAsync(cancellationToken: ct),
            cancellationToken);
        return new InspectionFileStreamResult(
            download.Value.Content,
            download.Value.Details.ContentType ?? "application/octet-stream",
            safeFileName);
    }

    private async Task<InspectionIngestOutboxEntity> CreateOrLoadOutboxEntityAsync(
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var entity = CreateOutboxEntity(request, manifest);

        try
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.InspectionIngestOutboxTableName,
                "AddInspectionIngestOutbox",
                ct => tableClient.AddEntityAsync(entity, ct),
                cancellationToken);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            var existing = await GetOutboxEntityAsync(request.SessionId ?? string.Empty, cancellationToken);
            ValidateEquivalentRequest(existing, request, manifest);
            return existing;
        }
    }

    private async Task<InspectionIngestOutboxEntity?> TryAcquireLeaseAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var entity = await TryGetOutboxEntityAsync(sessionId, cancellationToken);
        if (entity is null ||
            entity.Completed ||
            !entity.PayloadStaged ||
            !entity.FilesStaged ||
            entity.Processing ||
            (entity.LockedUntilUtc is not null && entity.LockedUntilUtc > DateTimeOffset.UtcNow) ||
            (entity.NextAttemptAtUtc is not null && entity.NextAttemptAtUtc > DateTimeOffset.UtcNow))
        {
            return null;
        }

        entity.Processing = true;
        entity.LockedUntilUtc = DateTimeOffset.UtcNow.Add(ProcessingLeaseDuration);
        entity.LastAttemptAtUtc = DateTimeOffset.UtcNow;
        entity.Status = "Processing";

        try
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.InspectionIngestOutboxTableName,
                "AcquireInspectionIngestLease",
                ct => tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct),
                cancellationToken);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409)
        {
            return null;
        }
    }

    private async Task<InspectionIngestOutboxEntity?> TryGetOutboxEntityAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            return await GetOutboxEntityAsync(sessionId, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<InspectionIngestOutboxEntity> GetOutboxEntityAsync(string sessionId, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var response = await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.InspectionIngestOutboxTableName,
            "GetInspectionIngestOutbox",
            ct => tableClient.GetEntityAsync<InspectionIngestOutboxEntity>(
                InspectionIngestOutboxEntity.PartitionKeyValue,
                sessionId,
                cancellationToken: ct),
            cancellationToken);
        return response.Value;
    }

    private Task SaveOutboxEntityAsync(InspectionIngestOutboxEntity entity, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        return _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.InspectionIngestOutboxTableName,
            "UpsertInspectionIngestOutbox",
            ct => tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct),
            cancellationToken);
    }

    private async Task SavePayloadAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            return;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobClient = containerClient.GetBlobClient($"{request.SessionId}.json");
        var payload = new InspectionPayload
        {
            SessionId = request.SessionId,
            UserId = request.UserId,
            Name = request.Name,
            QueryParams = request.QueryParams
        };

        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
        stream.Position = 0;
        await _storageObserver.ExecuteAsync(
            "blob",
            _blobOptions.ContainerName,
            "UploadInspectionPayload",
            ct => blobClient.UploadAsync(stream, overwrite: true, ct),
            cancellationToken);
    }

    private async Task SaveFilesAsync(
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest,
        CancellationToken cancellationToken)
    {
        if (request.Files is not { Length: > 0 })
        {
            return;
        }

        var sessionId = request.SessionId ?? string.Empty;
        var filesContainerClient = _blobServiceClient.GetBlobContainerClient(FilesContainerName);

        for (var i = 0; i < request.Files.Length; i++)
        {
            var file = request.Files[i];
            if (file is null || file.Length == 0)
            {
                continue;
            }

            var blobClient = filesContainerClient.GetBlobClient(manifest[i].BlobName);
            await using var fileStream = file.OpenReadStream();
            await _storageObserver.ExecuteAsync(
                "blob",
                FilesContainerName,
                "UploadInspectionFile",
                ct => blobClient.UploadAsync(fileStream, overwrite: true, ct),
                cancellationToken);
        }
    }

    private async Task SaveInspectionFilesMetadataAsync(
        InspectionIngestOutboxEntity entity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.InspectionFilesTableName))
        {
            return;
        }

        var sessionId = entity.SessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var files = DeserializeManifest(entity.FilesJson)
            .Select(file => new InspectionFileReference(file.FileName, sessionId, file.FileType))
            .ToArray();

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionFilesTableName);
        await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.InspectionFilesTableName,
            "UpsertInspectionFileMetadata",
            ct => tableClient.UpsertEntityAsync(
                new InspectionFilesEntity
                {
                    PartitionKey = InspectionFilesEntity.PartitionKeyValue,
                    RowKey = sessionId,
                    SessionId = sessionId,
                    Files = JsonSerializer.Serialize(files)
                },
                TableUpdateMode.Replace,
                ct),
            cancellationToken);
    }

    private Task SendQueueMessageAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_queueOptions.QueueName))
        {
            return Task.CompletedTask;
        }

        var queueClient = _queueServiceClient.GetQueueClient(_queueOptions.QueueName);
        return _storageObserver.ExecuteAsync(
            "queue",
            _queueOptions.QueueName,
            "SendInspectionQueueMessage",
            ct => queueClient.SendMessageAsync(
                JsonSerializer.Serialize(new { sessionId }),
                ct),
            cancellationToken);
    }

    private async Task CompensateStagingAsync(
        string sessionId,
        IReadOnlyCollection<InspectionIngestFileManifest> manifest,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
            var blobClient = containerClient.GetBlobClient($"{sessionId}.json");
            await _storageObserver.ExecuteAsync(
                "blob",
                _blobOptions.ContainerName,
                "DeleteInspectionPayloadCompensation",
                ct => blobClient.DeleteIfExistsAsync(cancellationToken: ct),
                cancellationToken);
        }

        var filesContainerClient = _blobServiceClient.GetBlobContainerClient(FilesContainerName);
        foreach (var file in manifest)
        {
            var blobClient = filesContainerClient.GetBlobClient(file.BlobName);
            await _storageObserver.ExecuteAsync(
                "blob",
                FilesContainerName,
                "DeleteInspectionFileCompensation",
                ct => blobClient.DeleteIfExistsAsync(cancellationToken: ct),
                cancellationToken);
        }
    }

    private async Task<InspectionPayload?> LoadInspectionPayloadAsync(
        BlobClient blobClient,
        CancellationToken cancellationToken)
    {
        var download = await _storageObserver.ExecuteAsync(
            "blob",
            _blobOptions.ContainerName,
            "DownloadInspectionPayload",
            ct => blobClient.DownloadContentAsync(ct),
            cancellationToken);
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
            var response = await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.InspectionFilesTableName,
                "GetInspectionFileMetadata",
                ct => tableClient.GetEntityAsync<InspectionFilesEntity>(
                    InspectionFilesEntity.PartitionKeyValue,
                    sessionId,
                    cancellationToken: ct),
                cancellationToken);

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

    private static ReceiveInspectionRequest NormalizeRequest(ReceiveInspectionRequest request)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("N")
            : request.SessionId;

        return request with
        {
            SessionId = sessionId,
            QueryParams = request.QueryParams ?? new Dictionary<string, string>()
        };
    }

    private static InspectionIngestOutboxEntity CreateOutboxEntity(
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest)
    {
        return new InspectionIngestOutboxEntity
        {
            RowKey = request.SessionId ?? string.Empty,
            SessionId = request.SessionId ?? string.Empty,
            UserId = request.UserId ?? string.Empty,
            Name = request.Name ?? string.Empty,
            QueryParamsJson = JsonSerializer.Serialize(request.QueryParams ?? new Dictionary<string, string>()),
            FilesJson = JsonSerializer.Serialize(manifest),
            NextAttemptAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static InspectionIngestFileManifest[] BuildManifest(ReceiveInspectionRequest request)
    {
        if (request.Files is not { Length: > 0 })
        {
            return Array.Empty<InspectionIngestFileManifest>();
        }

        var sessionId = request.SessionId ?? string.Empty;
        return request.Files
            .Select((file, index) =>
            {
                if (file is null || file.Length == 0)
                {
                    return null;
                }

                var fileName = InspectionFileName.Sanitize(file.FileName, index);
                return new InspectionIngestFileManifest(
                    fileName,
                    $"{sessionId}-{fileName}",
                    file.ContentType ?? string.Empty,
                    file.Length);
            })
            .Where(file => file is not null)
            .Cast<InspectionIngestFileManifest>()
            .ToArray();
    }

    private static InspectionIngestFileManifest[] DeserializeManifest(string filesJson)
    {
        return string.IsNullOrWhiteSpace(filesJson)
            ? Array.Empty<InspectionIngestFileManifest>()
            : JsonSerializer.Deserialize<InspectionIngestFileManifest[]>(
                filesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<InspectionIngestFileManifest>();
    }

    private static void ValidateEquivalentRequest(
        InspectionIngestOutboxEntity entity,
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest)
    {
        var existingFiles = DeserializeManifest(entity.FilesJson);
        var existingQueryParams = JsonSerializer.Deserialize<Dictionary<string, string>>(
                                     entity.QueryParamsJson,
                                     new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                                 new Dictionary<string, string>();
        var incomingQueryParams = request.QueryParams ?? new Dictionary<string, string>();

        var matches =
            string.Equals(entity.UserId, request.UserId ?? string.Empty, StringComparison.Ordinal) &&
            string.Equals(entity.Name, request.Name ?? string.Empty, StringComparison.Ordinal) &&
            JsonSerializer.Serialize(existingQueryParams) == JsonSerializer.Serialize(incomingQueryParams) &&
            JsonSerializer.Serialize(existingFiles) == JsonSerializer.Serialize(manifest);

        if (!matches)
        {
            throw new InvalidOperationException(
                $"Inspection ingest '{request.SessionId}' already exists with a different payload.");
        }
    }

    private static ReceiveInspectionResponse BuildResponse(ReceiveInspectionRequest request)
    {
        return new ReceiveInspectionResponse(
            "Received",
            request.SessionId ?? string.Empty,
            request.Name ?? string.Empty,
            request.QueryParams ?? new Dictionary<string, string>(),
            "Accepted for eventual processing");
    }

    private static TimeSpan GetRetryDelay(int retryCount)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Min(retryCount, 8)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private sealed class InspectionPayload
    {
        public string? SessionId { get; set; }
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
    }
}
