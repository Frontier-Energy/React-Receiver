using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class InspectionFilesEntity : ITableEntity
{
    public const string PartitionKeyValue = "InspectionFiles";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Files { get; set; } = "[]";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
