using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class FormSchemaCatalogItemEntity : ITableEntity
{
    public const string PartitionKeyValue = "FormSchemaCatalog";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Version { get; set; } = string.Empty;
    public string EtagValue { get; set; } = string.Empty;

    public FormSchemaCatalogItemResponse ToResponse()
    {
        return new FormSchemaCatalogItemResponse(
            FormType: RowKey,
            Version: Version,
            Etag: EtagValue);
    }

    public static FormSchemaCatalogItemEntity FromResponse(FormSchemaCatalogItemResponse response)
    {
        return new FormSchemaCatalogItemEntity
        {
            PartitionKey = PartitionKeyValue,
            RowKey = response.FormType,
            Version = response.Version,
            EtagValue = response.Etag
        };
    }
}
