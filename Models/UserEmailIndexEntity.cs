using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class UserEmailIndexEntity : ITableEntity
{
    public const string RowKeyPrefix = "email:";

    public string PartitionKey { get; set; } = UserEntity.PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string? Email { get; set; }

    public static string CreateRowKey(string email) =>
        $"{RowKeyPrefix}{email.Trim().ToUpperInvariant()}";
}
