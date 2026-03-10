using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Inspections;

internal sealed class AzureInspectionArtifactStore
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;
    private readonly IInspectionFileSecurityInspector _fileSecurityInspector;

    internal AzureInspectionArtifactStore(
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient,
        BlobStorageOptions blobOptions,
        TableStorageOptions tableOptions,
        IStorageOperationObserver storageObserver,
        IInspectionFileSecurityInspector fileSecurityInspector)
    {
        _blobServiceClient = blobServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions;
        _tableOptions = tableOptions;
        _storageObserver = storageObserver;
        _fileSecurityInspector = fileSecurityInspector;
    }

    internal async Task SavePayloadAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
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

    internal async Task SaveFilesAsync(
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest,
        CancellationToken cancellationToken)
    {
        if (request.Files is not { Length: > 0 })
        {
            return;
        }

        var quarantineContainerClient = _blobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesQuarantineContainerName);

        for (var i = 0; i < request.Files.Length; i++)
        {
            var file = request.Files[i];
            if (file is null || file.Length == 0)
            {
                continue;
            }

            var inspection = await _fileSecurityInspector.InspectAsync(file, cancellationToken);
            var verificationStatus = inspection.Accepted
                ? InspectionFileBlobMetadata.PendingStatus
                : InspectionFileBlobMetadata.RejectedStatus;
            var blobClient = quarantineContainerClient.GetBlobClient(manifest[i].BlobName);
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = InspectionFileBlobMetadata.CreateHeaders(inspection.DetectedContentType),
                Metadata = InspectionFileBlobMetadata.Create(
                    file.FileName,
                    file.ContentType ?? string.Empty,
                    inspection.DetectedContentType,
                    inspection.Sha256,
                    verificationStatus,
                    inspection.ScanEngine,
                    inspection.ScanDetails)
            };

            await _storageObserver.ExecuteAsync(
                "blob",
                StorageDependencyNames.FilesQuarantineContainerName,
                "UploadInspectionFileToQuarantine",
                ct => blobClient.UploadAsync(inspection.Content, uploadOptions, ct),
                cancellationToken);

            if (!inspection.Accepted)
            {
                throw new InspectionFileSecurityException(inspection.RejectionReason);
            }
        }
    }

    internal async Task VerifyAndPromoteFilesAsync(
        InspectionIngestOutboxEntity entity,
        CancellationToken cancellationToken)
    {
        var manifest = InspectionIngestStateMachine.DeserializeManifest(entity.FilesJson);
        if (manifest.Length == 0)
        {
            return;
        }

        var quarantineContainerClient = _blobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesQuarantineContainerName);
        var filesContainerClient = _blobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesContainerName);

        foreach (var file in manifest)
        {
            var quarantineBlobClient = quarantineContainerClient.GetBlobClient(file.BlobName);
            BlobProperties properties;
            try
            {
                properties = (await _storageObserver.ExecuteAsync(
                    "blob",
                    StorageDependencyNames.FilesQuarantineContainerName,
                    "GetQuarantinedInspectionFileProperties",
                    ct => quarantineBlobClient.GetPropertiesAsync(cancellationToken: ct),
                    cancellationToken)).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                throw new InvalidOperationException($"Quarantined file '{file.FileName}' was not found for session '{entity.SessionId}'.", ex);
            }

            var metadata = properties.Metadata;
            var verificationStatus = metadata.TryGetValue(InspectionFileBlobMetadata.VerificationStatusKey, out var status)
                ? status
                : string.Empty;
            if (string.Equals(verificationStatus, InspectionFileBlobMetadata.RejectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                throw new InspectionFileSecurityException(
                    $"File '{file.FileName}' was rejected during quarantine verification and cannot be promoted.");
            }

            var download = await _storageObserver.ExecuteAsync(
                "blob",
                StorageDependencyNames.FilesQuarantineContainerName,
                "DownloadQuarantinedInspectionFile",
                ct => quarantineBlobClient.DownloadContentAsync(ct),
                cancellationToken);
            var finalBlobClient = filesContainerClient.GetBlobClient(file.BlobName);
            var detectedContentType = metadata.TryGetValue(InspectionFileBlobMetadata.DetectedContentTypeKey, out var detected)
                ? detected
                : properties.ContentType ?? file.FileType;
            var finalUploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = string.IsNullOrWhiteSpace(detectedContentType)
                        ? "application/octet-stream"
                        : detectedContentType
                },
                Metadata = InspectionFileBlobMetadata.Create(
                    metadata.TryGetValue(InspectionFileBlobMetadata.OriginalFileNameKey, out var originalFileName) ? originalFileName : file.FileName,
                    metadata.TryGetValue(InspectionFileBlobMetadata.OriginalContentTypeKey, out var originalContentType) ? originalContentType : file.FileType,
                    detectedContentType,
                    metadata.TryGetValue(InspectionFileBlobMetadata.Sha256Key, out var sha256) ? sha256 : string.Empty,
                    InspectionFileBlobMetadata.VerifiedStatus,
                    metadata.TryGetValue(InspectionFileBlobMetadata.ScanEngineKey, out var scanEngine) ? scanEngine : string.Empty,
                    metadata.TryGetValue(InspectionFileBlobMetadata.ScanDetailsKey, out var scanDetails) ? scanDetails : string.Empty)
            };

            await _storageObserver.ExecuteAsync(
                "blob",
                StorageDependencyNames.FilesContainerName,
                "PromoteVerifiedInspectionFile",
                ct => finalBlobClient.UploadAsync(download.Value.Content, finalUploadOptions, ct),
                cancellationToken);
            await _storageObserver.ExecuteAsync(
                "blob",
                StorageDependencyNames.FilesQuarantineContainerName,
                "DeleteVerifiedInspectionFileFromQuarantine",
                ct => quarantineBlobClient.DeleteIfExistsAsync(cancellationToken: ct),
                cancellationToken);
        }
    }

    internal async Task CompensateStagingAsync(
        string sessionId,
        IReadOnlyCollection<InspectionIngestFileManifest> manifest,
        bool deleteQuarantineFiles,
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

        if (deleteQuarantineFiles)
        {
            var quarantineContainerClient = _blobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesQuarantineContainerName);
            foreach (var file in manifest)
            {
                var blobClient = quarantineContainerClient.GetBlobClient(file.BlobName);
                await _storageObserver.ExecuteAsync(
                    "blob",
                    StorageDependencyNames.FilesQuarantineContainerName,
                    "DeleteInspectionFileQuarantineCompensation",
                    ct => blobClient.DeleteIfExistsAsync(cancellationToken: ct),
                    cancellationToken);
            }
        }

        var filesContainerClient = _blobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesContainerName);
        foreach (var file in manifest)
        {
            var blobClient = filesContainerClient.GetBlobClient(file.BlobName);
            await _storageObserver.ExecuteAsync(
                "blob",
                StorageDependencyNames.FilesContainerName,
                "DeleteInspectionFileCompensation",
                ct => blobClient.DeleteIfExistsAsync(cancellationToken: ct),
                cancellationToken);
        }
    }

    internal async Task<GetInspectionResponse?> GetInspectionAsync(string sessionId, CancellationToken cancellationToken)
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

    internal async Task<InspectionFileStreamResult?> GetFileAsync(
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

        var containerClient = _blobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesContainerName);
        var blobClient = containerClient.GetBlobClient($"{sessionId}-{safeFileName}");
        if (!await _storageObserver.ExecuteAsync(
                "blob",
                StorageDependencyNames.FilesContainerName,
                "InspectionFileExists",
                ct => blobClient.ExistsAsync(ct),
                cancellationToken))
        {
            return null;
        }

        var download = await _storageObserver.ExecuteAsync(
            "blob",
            StorageDependencyNames.FilesContainerName,
            "DownloadInspectionFile",
            ct => blobClient.DownloadStreamingAsync(cancellationToken: ct),
            cancellationToken);
        return new InspectionFileStreamResult(
            download.Value.Content,
            download.Value.Details.ContentType ?? "application/octet-stream",
            safeFileName);
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

    private sealed class InspectionPayload
    {
        public string? SessionId { get; set; }
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
    }
}
