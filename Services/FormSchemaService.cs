using System.Collections.Concurrent;
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
}

public sealed class FormSchemaService : IFormSchemaService
{
    private sealed record FormSchemaCatalogEntry(
        string Version,
        string Etag,
        FormSchemaResponse Schema
    )
    {
        public FormSchemaCatalogItemResponse ToCatalogItem(string formType) =>
            new(
                FormType: formType,
                Version: Version,
                Etag: Etag);
    }

    private static readonly ConcurrentDictionary<string, FormSchemaCatalogEntry> FormSchemaCatalog =
        new(
            new Dictionary<string, FormSchemaCatalogEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["hvac"] = new(
                    Version: "2026-03-05",
                    Etag: "\"hvac-v1\"",
                    Schema: new FormSchemaResponse(
                        FormName: "HVAC Inspection",
                        Sections:
                        [
                            new FormSectionResponse(
                                Title: "Equipment Information",
                                Fields:
                                [
                                    new FormFieldResponse(
                                        Id: "unitLocation",
                                        Label: "Unit Location",
                                        Type: "text",
                                        Required: true,
                                        ExternalID: "hvac.unitLocation",
                                        Placeholder: "e.g., Attic, Basement, Roof",
                                        ValidationRules:
                                        [
                                            new ValidationRuleResponse(
                                                Type: "minLength",
                                                Value: 3,
                                                Message: "Unit location must be at least 3 characters")
                                        ])
                                ])
                        ])),
                ["electrical"] = new(
                    Version: "2026-03-05",
                    Etag: "\"electrical-v1\"",
                    Schema: new FormSchemaResponse(
                        FormName: "Electrical Inspection",
                        Sections:
                        [
                            new FormSectionResponse(
                                Title: "General",
                                Fields:
                                [
                                    new FormFieldResponse(
                                        Id: "panelCondition",
                                        Label: "Panel Condition",
                                        Type: "select",
                                        Required: true,
                                        ExternalID: "electrical.panelCondition",
                                        Options:
                                        [
                                            new FormFieldOptionResponse("Good", "good"),
                                            new FormFieldOptionResponse("Needs Attention", "needs-attention")
                                        ])
                                ])
                        ])),
                ["electrical-sf"] = new(
                    Version: "2026-03-05",
                    Etag: "\"electrical-sf-v1\"",
                    Schema: new FormSchemaResponse(
                        FormName: "Electrical SF Inspection",
                        Sections:
                        [
                            new FormSectionResponse(
                                Title: "Service",
                                Fields:
                                [
                                    new FormFieldResponse(
                                        Id: "serviceAmps",
                                        Label: "Service Amperage",
                                        Type: "number",
                                        Required: true,
                                        ExternalID: "electrical.serviceAmps")
                                ])
                        ])),
                ["safety-checklist"] = new(
                    Version: "2026-03-05",
                    Etag: "\"safety-checklist-v1\"",
                    Schema: new FormSchemaResponse(
                        FormName: "Safety Checklist",
                        Sections:
                        [
                            new FormSectionResponse(
                                Title: "Site Safety",
                                Fields:
                                [
                                    new FormFieldResponse(
                                        Id: "ppeVerified",
                                        Label: "PPE Verified",
                                        Type: "checkbox",
                                        Required: true,
                                        ExternalID: "safety.ppeVerified")
                                ])
                        ]))
            });

    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly BlobStorageOptions _blobOptions;

    public FormSchemaService(
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

    public async Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken)
    {
        var defaultItems = FormSchemaCatalog
            .Select(item => item.Value.ToCatalogItem(item.Key))
            .ToArray();

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.FormSchemaCatalogTableName))
        {
            return new FormSchemaCatalogResponse(Items: defaultItems);
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

        if (items.Count == 0)
        {
            foreach (var defaultItem in defaultItems)
            {
                await tableClient.UpsertEntityAsync(
                    FormSchemaCatalogItemEntity.FromResponse(defaultItem),
                    TableUpdateMode.Replace,
                    cancellationToken);
            }

            items.AddRange(defaultItems);
        }

        return new FormSchemaCatalogResponse(Items: items.ToArray());
    }

    public async Task<FormSchemaResponse?> GetAsync(string formType, CancellationToken cancellationToken)
    {
        if (!FormSchemaCatalog.TryGetValue(formType, out var defaultSchema))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.FormSchemasTableName))
        {
            return defaultSchema.Schema;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.FormSchemasTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        try
        {
            var response = await tableClient.GetEntityAsync<FormSchemaEntity>(
                FormSchemaEntity.PartitionKeyValue,
                formType,
                cancellationToken: cancellationToken);
            return await ReadSchemaAsync(response.Value, defaultSchema.Schema, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            await UpsertSchemaEntityAsync(tableClient, formType, defaultSchema.Schema, cancellationToken);
            return defaultSchema.Schema;
        }
    }

    public async Task<FormSchemaResponse?> UpsertAsync(
        string formType,
        FormSchemaResponse request,
        CancellationToken cancellationToken)
    {
        if (!FormSchemaCatalog.ContainsKey(formType))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var catalogItem = new FormSchemaCatalogItemResponse(
            FormType: formType,
            Version: now.ToString("yyyy-MM-dd"),
            Etag: $"\"{formType}-{now:yyyyMMddHHmmssfff}\"");

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.FormSchemasTableName) ||
            string.IsNullOrWhiteSpace(_tableOptions.FormSchemaCatalogTableName))
        {
            FormSchemaCatalog[formType] = new FormSchemaCatalogEntry(
                Version: catalogItem.Version,
                Etag: catalogItem.Etag,
                Schema: request);
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

        FormSchemaCatalog[formType] = new FormSchemaCatalogEntry(
            Version: catalogItem.Version,
            Etag: catalogItem.Etag,
            Schema: request);

        return request;
    }

    private async Task<FormSchemaResponse> ReadSchemaAsync(
        FormSchemaEntity entity,
        FormSchemaResponse fallbackSchema,
        CancellationToken cancellationToken)
    {
        if (entity.HasSchemaBlob)
        {
            return await DownloadSchemaAsync(entity.SchemaBlobName, fallbackSchema, cancellationToken);
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
        FormSchemaResponse fallbackSchema,
        CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobOptions.ContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Form schema blob '{blobName}' was not found.");
        }

        var content = await blobClient.DownloadContentAsync(cancellationToken);
        return content.Value.Content.ToObjectFromJson<FormSchemaResponse>() ?? fallbackSchema;
    }

    private bool HasBlobStorageConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_blobOptions.ConnectionString) &&
               !string.IsNullOrWhiteSpace(_blobOptions.ContainerName);
    }

    private static string GetSchemaBlobName(string formType)
    {
        return $"form-schemas/{formType}.json";
    }
}
