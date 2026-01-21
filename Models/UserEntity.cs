using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class UserEntity : ITableEntity
{
    public const string PartitionKeyValue = "Users";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
