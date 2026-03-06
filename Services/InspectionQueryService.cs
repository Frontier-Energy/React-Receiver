using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using React_Receiver.Models;

namespace React_Receiver.Services;

public interface IInspectionQueryService
{
    Task<GetInspectionResponse?> GetInspectionAsync(string sessionId, CancellationToken cancellationToken);
    Task<InspectionFileStreamResult?> GetFileAsync(string sessionId, string fileName, CancellationToken cancellationToken);
}

public sealed record InspectionFileStreamResult(
    Stream Content,
    string ContentType,
    string FileName
);

public sealed class InspectionQueryService : IInspectionQueryService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly TableStorageOptions _tableOptions;

    public InspectionQueryService(
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _blobServiceClient = blobServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions.Value;
        _tableOptions = tableOptions.Value;
    }

    public async Task<GetInspectionResponse?> GetInspectionAsync(string sessionId, CancellationToken cancellationToken)
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
            SessionId: payload.SessionId ?? sessionId,
            UserId: payload.UserId,
            Name: payload.Name,
            QueryParams: payload.QueryParams ?? new Dictionary<string, string>(),
            Files: files);
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

        var containerClient = _blobServiceClient.GetBlobContainerClient("files");
        var blobClient = containerClient.GetBlobClient($"{sessionId}-{safeFileName}");

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new InspectionFileStreamResult(
            Content: download.Value.Content,
            ContentType: download.Value.Details.ContentType ?? "application/octet-stream",
            FileName: safeFileName);
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

    private sealed class InspectionPayload
    {
        public string? SessionId { get; set; }
        public string? UserId { get; set; }
        public string? Name { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
    }
}
