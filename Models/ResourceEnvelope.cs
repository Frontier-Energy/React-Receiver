namespace React_Receiver.Models;

public sealed record ResourceEnvelope<TResource>(
    TResource Resource,
    string? ETag = null,
    string? Version = null);
