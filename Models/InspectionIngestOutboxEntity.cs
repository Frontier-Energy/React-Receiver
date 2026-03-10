using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class InspectionIngestOutboxEntity : ITableEntity
{
    public const string PartitionKeyValue = "InspectionIngest";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string QueryParamsJson { get; set; } = "{}";
    public string FilesJson { get; set; } = "[]";
    public bool PayloadStaged { get; set; }
    public bool FilesStaged { get; set; }
    public bool FilesVerified { get; set; }
    public bool MetadataWritten { get; set; }
    public bool QueueMessageSent { get; set; }
    public bool Completed { get; set; }
    public bool Processing { get; set; }
    public int RetryCount { get; set; }
    public string Status { get; set; } = "Received";
    public string LastError { get; set; } = string.Empty;
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

public sealed record InspectionIngestFileManifest(
    string FileName,
    string BlobName,
    string FileType,
    long Length
);
