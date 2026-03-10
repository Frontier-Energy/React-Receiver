namespace React_Receiver.Models;

public sealed record InspectionIngestProcessResult(
    bool Processed,
    string Status,
    bool TerminalFailure,
    int RetryCount,
    string LastError);

public sealed record InspectionIngestOutboxSessionSummary(
    string SessionId,
    string UserId,
    string Name,
    string Status,
    bool Completed,
    bool TerminalFailure,
    bool CanReplay,
    int RetryCount,
    string LastError,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset? PoisonedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record InspectionIngestOutboxSessionDetail(
    string SessionId,
    string UserId,
    string Name,
    IReadOnlyDictionary<string, string> QueryParams,
    IReadOnlyCollection<InspectionIngestFileManifest> Files,
    string Status,
    bool PayloadStaged,
    bool FilesStaged,
    bool FilesVerified,
    bool MetadataWritten,
    bool QueueMessageSent,
    bool Completed,
    bool TerminalFailure,
    bool Processing,
    bool CanReplay,
    int RetryCount,
    string LastError,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset? PoisonedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record ReplayInspectionIngestSessionResponse(
    bool Accepted,
    string Message,
    InspectionIngestOutboxSessionDetail? Session);
