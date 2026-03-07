namespace React_Receiver.Models;

public sealed record UpsertResult<TResource>(
    TResource Resource,
    bool Created,
    string? Version = null,
    string? ETag = null);
