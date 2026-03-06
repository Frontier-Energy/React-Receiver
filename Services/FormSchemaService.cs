using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using React_Receiver.Models;

namespace React_Receiver.Services;

public interface IFormSchemaService
{
    Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken);
    Task<FormSchemaResponse?> GetAsync(string formType, CancellationToken cancellationToken);
    Task<FormSchemaResponse?> UpsertAsync(string formType, FormSchemaResponse request, CancellationToken cancellationToken);
    Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken);
}

public sealed class FormSchemaService : IFormSchemaService
{
    private sealed record FormSchemaCatalogEntry(
        string Version,
        string Etag,
        FormSchemaResponse Schema)
    {
        public FormSchemaCatalogItemResponse ToCatalogItem(string formType) =>
            new(formType, Version, Etag);
    }

    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly BlobStorageOptions _blobOptions;
    private readonly Dictionary<string, FormSchemaCatalogEntry> _formSchemaCatalog;

    public FormSchemaService(
        BlobServiceClient blobServiceClient,
        TableServiceClient tableServiceClient,
        IBootstrapDataProvider bootstrapDataProvider,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _blobServiceClient = blobServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions.Value;
        _tableOptions = tableOptions.Value;
        _formSchemaCatalog = bootstrapDataProvider
            .GetFormSchemas()
            .ToDictionary(
                item => item.FormType,
                item => new FormSchemaCatalogEntry(item.Version, item.Etag, item.Schema),
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken)
    {
        var defaultItems = _formSchemaCatalog
            .Select(item => item.Value.ToCatalogItem(item.Key))
            .ToArray();

        if (!HasTableStorageConfiguration())
        {
            return new FormSchemaCatalogResponse(defaultItems);
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemaCatalogTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var items = new List<FormSchemaCatalogItemResponse>();
        await foreach (var entity in tableClient.QueryAsync<FormSchemaCatalogItemEntity>(
                           e => e.PartitionKey == FormSchemaCatalogItemEntity.PartitionKeyValue,
                           cancellationToken: cancellationToken))
        {
            items.Add(entity.ToResponse());
        }

        return new FormSchemaCatalogResponse(items.ToArray());
    }

    public async Task<FormSchemaResponse?> GetAsync(string formType, CancellationToken cancellationToken)
    {
        if (!HasTableStorageConfiguration())
        {
            return _formSchemaCatalog.TryGetValue(formType, out var defaultSchema)
                ? defaultSchema.Schema
                : null;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        try
        {
            var response = await tableClient.GetEntityAsync<FormSchemaEntity>(
                FormSchemaEntity.PartitionKeyValue,
                formType,
                cancellationToken: cancellationToken);
            return await ReadSchemaAsync(response.Value, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<FormSchemaResponse?> UpsertAsync(
        string formType,
        FormSchemaResponse request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var catalogItem = new FormSchemaCatalogItemResponse(
            formType,
            now.ToString("yyyy-MM-dd"),
            $"\"{formType}-{now:yyyyMMddHHmmssfff}\"");

        if (!HasTableStorageConfiguration())
        {
            _formSchemaCatalog[formType] = new FormSchemaCatalogEntry(
                catalogItem.Version,
                catalogItem.Etag,
                request);
            return request;
        }

        var schemasTableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        await schemasTableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await UpsertSchemaEntityAsync(schemasTableClient, formType, request, cancellationToken);

        var catalogTableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemaCatalogTableName);
        await catalogTableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await catalogTableClient.UpsertEntityAsync(
            FormSchemaCatalogItemEntity.FromResponse(catalogItem),
            TableUpdateMode.Replace,
            cancellationToken);

        _formSchemaCatalog[formType] = new FormSchemaCatalogEntry(
            catalogItem.Version,
            catalogItem.Etag,
            request);

        return request;
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!HasTableStorageConfiguration())
        {
            return;
        }

        var schemasTableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        await schemasTableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var catalogTableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemaCatalogTableName);
        await catalogTableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        foreach (var seed in _formSchemaCatalog)
        {
            if (!overwriteExisting && await SchemaExistsAsync(schemasTableClient, seed.Key, cancellationToken))
            {
                continue;
            }

            await UpsertSchemaEntityAsync(schemasTableClient, seed.Key, seed.Value.Schema, cancellationToken);
            await catalogTableClient.UpsertEntityAsync(
                FormSchemaCatalogItemEntity.FromResponse(seed.Value.ToCatalogItem(seed.Key)),
                TableUpdateMode.Replace,
                cancellationToken);
        }
    }

    private async Task<FormSchemaResponse> ReadSchemaAsync(
        FormSchemaEntity entity,
        CancellationToken cancellationToken)
    {
        if (entity.HasSchemaBlob)
        {
            return await DownloadSchemaAsync(entity.SchemaBlobName, cancellationToken);
        }

        return entity.ToResponse();
    }

    private async Task UpsertSchemaEntityAsync(
        TableClient tableClient,
        string formType,
        FormSchemaResponse schema,
        CancellationToken cancellationToken)
    {
        var schemaBlobName = HasBlobStorageConfiguration()
            ? await UploadSchemaAsync(formType, schema, cancellationToken)
            : null;

        await tableClient.UpsertEntityAsync(
            FormSchemaEntity.FromResponse(formType, schema, schemaBlobName),
            TableUpdateMode.Replace,
            cancellationToken);
    }

    private async Task<string> UploadSchemaAsync(
        string formType,
        FormSchemaResponse schema,
        CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = GetSchemaBlobName(formType);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(BinaryData.FromObjectAsJson(schema), overwrite: true, cancellationToken);

        return blobName;
    }

    private async Task<FormSchemaResponse> DownloadSchemaAsync(
        string blobName,
        CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Form schema blob '{blobName}' was not found.");
        }

        var content = await blobClient.DownloadContentAsync(cancellationToken);
        return content.Value.Content.ToObjectFromJson<FormSchemaResponse>()
            ?? throw new InvalidOperationException($"Form schema blob '{blobName}' did not contain a valid payload.");
    }

    private bool HasBlobStorageConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_blobOptions.ConnectionString) &&
               !string.IsNullOrWhiteSpace(_blobOptions.ContainerName);
    }

    private bool HasTableStorageConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
               !string.IsNullOrWhiteSpace(_tableOptions.FormSchemasTableName) &&
               !string.IsNullOrWhiteSpace(_tableOptions.FormSchemaCatalogTableName);
    }

    private static async Task<bool> SchemaExistsAsync(
        TableClient tableClient,
        string formType,
        CancellationToken cancellationToken)
    {
        try
        {
            await tableClient.GetEntityAsync<FormSchemaEntity>(
                FormSchemaEntity.PartitionKeyValue,
                formType,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static string GetSchemaBlobName(string formType)
    {
        return $"form-schemas/{formType}.json";
    }
}
