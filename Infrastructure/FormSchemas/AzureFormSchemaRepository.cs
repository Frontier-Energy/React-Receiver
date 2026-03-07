using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using React_Receiver.Application.Concurrency;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.FormSchemas;

public sealed class AzureFormSchemaRepository : IFormSchemaRepository
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly BlobStorageOptions _blobOptions;
    private readonly ILogger<AzureFormSchemaRepository> _logger;
    private readonly IStorageOperationObserver _storageObserver;

    public AzureFormSchemaRepository(
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient,
        ILogger<AzureFormSchemaRepository> logger,
        IStorageOperationObserver storageObserver,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _blobServiceClient = blobServiceClient;
        _tableServiceClient = tableServiceClient;
        _logger = logger;
        _storageObserver = storageObserver;
        _blobOptions = blobOptions.Value;
        _tableOptions = tableOptions.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
        !string.IsNullOrWhiteSpace(_tableOptions.FormSchemasTableName) &&
        !string.IsNullOrWhiteSpace(_tableOptions.FormSchemaCatalogTableName);

    public async Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        return await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.FormSchemasTableName,
            "ListFormSchemas",
            async ct =>
            {
                var items = new List<FormSchemaCatalogItemResponse>();
                await foreach (var entity in tableClient.QueryAsync<FormSchemaEntity>(
                                   e => e.PartitionKey == FormSchemaEntity.PartitionKeyValue,
                                   cancellationToken: ct))
                {
                    items.Add(entity.ToCatalogItem());
                }

                return new FormSchemaCatalogResponse(items.ToArray());
            },
            cancellationToken);
    }

    public async Task<ResourceEnvelope<FormSchemaResponse>?> GetAsync(string formType, CancellationToken cancellationToken)
    {
        var state = await TryGetStateAsync(formType, cancellationToken);
        if (state is null)
        {
            return null;
        }

        var resource = await ReadSchemaAsync(state.SchemaEntity, cancellationToken);
        return new ResourceEnvelope<FormSchemaResponse>(resource, state.SchemaEntity.PublicEtag, state.SchemaEntity.Version);
    }

    public async Task<bool> ExistsAsync(string formType, CancellationToken cancellationToken)
    {
        return await TryGetStateAsync(formType, cancellationToken) is not null;
    }

    public async Task<UpsertResult<FormSchemaResponse>> UpsertAsync(
        string formType,
        FormSchemaResponse request,
        string? expectedETag,
        CancellationToken cancellationToken)
    {
        EnsureBlobStorageConfigured();

        var existing = await TryGetStateAsync(formType, cancellationToken);
        OptimisticConcurrency.EnsureSatisfied(expectedETag, existing?.SchemaEntity.PublicEtag, $"form schema '{formType}'");

        var now = DateTime.UtcNow;
        var version = now.ToString("yyyy-MM-dd");
        var publicEtag = $"\"{formType}-{now:yyyyMMddHHmmssfff}\"";
        var schemaBlobName = await UploadSchemaAsync(formType, publicEtag, request, cancellationToken);
        var schemaEntity = FormSchemaEntity.FromResponse(formType, schemaBlobName, version, publicEtag);

        var schemasTableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        if (existing is null)
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.FormSchemasTableName,
                "AddFormSchemaMetadata",
                ct => schemasTableClient.AddEntityAsync(schemaEntity, ct),
                cancellationToken);
        }
        else
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.FormSchemasTableName,
                "UpdateFormSchemaMetadata",
                ct => schemasTableClient.UpdateEntityAsync(schemaEntity, existing.SchemaEntity.ETag, TableUpdateMode.Replace, ct),
                cancellationToken);
        }

        await UpsertCatalogCopyAsync(schemaEntity, cancellationToken);
        return new UpsertResult<FormSchemaResponse>(request, existing is null, version, publicEtag);
    }

    public async Task<FormSchemaResponse> ReadSchemaAsync(
        FormSchemaEntity entity,
        CancellationToken cancellationToken)
    {
        if (entity.HasSchemaBlob)
        {
            if (!HasBlobStorageConfiguration())
            {
                if (entity.HasInlineSections)
                {
                    _logger.LogWarning(
                        "Blob storage is not configured for schema '{FormType}'. Falling back to inline schema payload.",
                        entity.RowKey);
                    return entity.ToResponse();
                }

                throw new FormSchemaBlobContentException(
                    $"Form schema '{entity.RowKey}' metadata exists, but blob storage is not configured.");
            }

            try
            {
                return await DownloadSchemaAsync(entity.SchemaBlobName, cancellationToken);
            }
            catch (FormSchemaBlobContentException ex) when (entity.HasInlineSections)
            {
                _logger.LogWarning(
                    ex,
                    "Blob-backed schema content could not be read for '{FormType}'. Falling back to inline schema payload.",
                    entity.RowKey);
                return entity.ToResponse();
            }
        }

        if (entity.HasInlineSections)
        {
            return entity.ToResponse();
        }

        throw new FormSchemaBlobContentException(
            $"Form schema '{entity.RowKey}' metadata exists, but does not contain inline content or a blob reference.");
    }

    private async Task<SchemaState?> TryGetStateAsync(string formType, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        try
        {
            var response = await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.FormSchemasTableName,
                "GetFormSchema",
                ct => tableClient.GetEntityAsync<FormSchemaEntity>(
                    FormSchemaEntity.PartitionKeyValue,
                    formType,
                    cancellationToken: ct),
                cancellationToken);
            return new SchemaState(response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Form schema metadata was not found for form type '{FormType}'.", formType);
            return null;
        }
    }

    private async Task UpsertCatalogCopyAsync(FormSchemaEntity schemaEntity, CancellationToken cancellationToken)
    {
        var catalogTableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemaCatalogTableName);
        await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.FormSchemaCatalogTableName,
            "UpsertFormSchemaCatalog",
            ct => catalogTableClient.UpsertEntityAsync(
                FormSchemaCatalogItemEntity.FromResponse(schemaEntity.ToCatalogItem()),
                TableUpdateMode.Replace,
                ct),
            cancellationToken);
    }

    private async Task<string> UploadSchemaAsync(
        string formType,
        string publicEtag,
        FormSchemaResponse schema,
        CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobName = $"form-schemas/{formType}/{SanitizeEtag(publicEtag)}.json";
        var blobClient = containerClient.GetBlobClient(blobName);
        await _storageObserver.ExecuteAsync(
            "blob",
            _blobOptions.ContainerName,
            "UploadFormSchema",
            ct => blobClient.UploadAsync(BinaryData.FromObjectAsJson(schema), overwrite: false, ct),
            cancellationToken);
        return blobName;
    }

    private async Task<FormSchemaResponse> DownloadSchemaAsync(string blobName, CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        try
        {
            if (!await _storageObserver.ExecuteAsync(
                    "blob",
                    _blobOptions.ContainerName,
                    "FormSchemaBlobExists",
                    ct => blobClient.ExistsAsync(ct),
                    cancellationToken))
            {
                throw new FormSchemaBlobContentException($"Form schema blob '{blobName}' was not found.");
            }

            var content = await _storageObserver.ExecuteAsync(
                "blob",
                _blobOptions.ContainerName,
                "DownloadFormSchema",
                ct => blobClient.DownloadContentAsync(ct),
                cancellationToken);
            return content.Value.Content.ToObjectFromJson<FormSchemaResponse>()
                ?? throw new FormSchemaBlobContentException($"Form schema blob '{blobName}' did not contain a valid payload.");
        }
        catch (FormSchemaBlobContentException)
        {
            throw;
        }
        catch (RequestFailedException ex)
        {
            throw new FormSchemaBlobContentException(
                $"Form schema blob '{blobName}' could not be read from blob storage.",
                ex);
        }
    }

    private bool HasBlobStorageConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_blobOptions.ConnectionString) &&
               !string.IsNullOrWhiteSpace(_blobOptions.ContainerName);
    }

    private void EnsureBlobStorageConfigured()
    {
        if (!HasBlobStorageConfiguration())
        {
            throw new InvalidOperationException(
                "Blob storage configuration is required for persisted form schema payloads.");
        }
    }

    private static string SanitizeEtag(string publicEtag)
    {
        return publicEtag.Replace("\"", string.Empty, StringComparison.Ordinal);
    }

    private sealed record SchemaState(FormSchemaEntity SchemaEntity);
}
