using System.Text.Json;
using React_Receiver.Domain.Inspections;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Models;

namespace React_Receiver.Infrastructure.Inspections;

internal static class InspectionIngestStateMachine
{
    internal static ReceiveInspectionRequest NormalizeRequest(ReceiveInspectionRequest request)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("N")
            : request.SessionId;

        return request with
        {
            SessionId = sessionId,
            QueryParams = request.QueryParams ?? new Dictionary<string, string>()
        };
    }

    internal static InspectionIngestOutboxEntity CreateOutboxEntity(
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest,
        DateTimeOffset nowUtc)
    {
        return new InspectionIngestOutboxEntity
        {
            RowKey = request.SessionId ?? string.Empty,
            SessionId = request.SessionId ?? string.Empty,
            UserId = request.UserId ?? string.Empty,
            Name = request.Name ?? string.Empty,
            QueryParamsJson = JsonSerializer.Serialize(request.QueryParams ?? new Dictionary<string, string>()),
            FilesJson = JsonSerializer.Serialize(manifest),
            NextAttemptAtUtc = nowUtc
        };
    }

    internal static InspectionIngestFileManifest[] BuildManifest(ReceiveInspectionRequest request)
    {
        if (request.Files is not { Length: > 0 })
        {
            return Array.Empty<InspectionIngestFileManifest>();
        }

        var sessionId = request.SessionId ?? string.Empty;
        return request.Files
            .Select((file, index) =>
            {
                if (file is null || file.Length == 0)
                {
                    return null;
                }

                var fileName = InspectionFileName.Sanitize(file.FileName, index);
                return new InspectionIngestFileManifest(
                    fileName,
                    $"{sessionId}-{fileName}",
                    file.ContentType ?? string.Empty,
                    file.Length);
            })
            .Where(file => file is not null)
            .Cast<InspectionIngestFileManifest>()
            .ToArray();
    }

    internal static InspectionIngestFileManifest[] DeserializeManifest(string filesJson)
    {
        return string.IsNullOrWhiteSpace(filesJson)
            ? Array.Empty<InspectionIngestFileManifest>()
            : JsonSerializer.Deserialize<InspectionIngestFileManifest[]>(
                filesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<InspectionIngestFileManifest>();
    }

    internal static void ValidateEquivalentRequest(
        InspectionIngestOutboxEntity entity,
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest)
    {
        var existingFiles = DeserializeManifest(entity.FilesJson);
        var existingQueryParams = JsonSerializer.Deserialize<Dictionary<string, string>>(
                                     entity.QueryParamsJson,
                                     new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                                 new Dictionary<string, string>();
        var incomingQueryParams = request.QueryParams ?? new Dictionary<string, string>();

        var matches =
            string.Equals(entity.UserId, request.UserId ?? string.Empty, StringComparison.Ordinal) &&
            string.Equals(entity.Name, request.Name ?? string.Empty, StringComparison.Ordinal) &&
            QueryParamsMatch(existingQueryParams, incomingQueryParams) &&
            FilesMatch(existingFiles, manifest);

        if (!matches)
        {
            throw new DuplicateInspectionSessionException(
                $"Inspection ingest '{request.SessionId}' already exists with different normalized payload or file metadata.");
        }
    }

    internal static bool IsPreparationComplete(InspectionIngestOutboxEntity entity)
    {
        return entity.Completed || (entity.PayloadStaged && entity.FilesStaged);
    }

    internal static bool IsReadyToAcquireLease(InspectionIngestOutboxEntity? entity, DateTimeOffset nowUtc)
    {
        return entity is not null &&
               !entity.Completed &&
               entity.PayloadStaged &&
               entity.FilesStaged &&
               !entity.Processing &&
               (entity.LockedUntilUtc is null || entity.LockedUntilUtc <= nowUtc) &&
               (entity.NextAttemptAtUtc is null || entity.NextAttemptAtUtc <= nowUtc);
    }

    internal static bool IsPending(InspectionIngestOutboxEntity entity, DateTimeOffset nowUtc)
    {
        return !entity.Completed &&
               entity.PayloadStaged &&
               entity.FilesStaged &&
               !entity.Processing &&
               (entity.LockedUntilUtc is null || entity.LockedUntilUtc <= nowUtc) &&
               (entity.NextAttemptAtUtc is null || entity.NextAttemptAtUtc <= nowUtc);
    }

    internal static void MarkPayloadStaged(InspectionIngestOutboxEntity entity, DateTimeOffset nowUtc)
    {
        entity.PayloadStaged = true;
        entity.Status = "PayloadStaged";
        entity.LastError = string.Empty;
        entity.NextAttemptAtUtc = nowUtc;
    }

    internal static void MarkFilesStaged(InspectionIngestOutboxEntity entity, DateTimeOffset nowUtc)
    {
        entity.FilesStaged = true;
        entity.Status = "Quarantined";
        entity.LastError = string.Empty;
        entity.NextAttemptAtUtc = nowUtc;
    }

    internal static void MarkFilesVerified(InspectionIngestOutboxEntity entity, DateTimeOffset nowUtc)
    {
        entity.FilesVerified = true;
        entity.Status = "FilesVerified";
        entity.LastError = string.Empty;
        entity.NextAttemptAtUtc = nowUtc;
    }

    internal static void MarkCompensated(InspectionIngestOutboxEntity entity, Exception exception)
    {
        entity.PayloadStaged = false;
        entity.FilesStaged = false;
        entity.FilesVerified = false;
        entity.MetadataWritten = false;
        entity.QueueMessageSent = false;
        entity.Completed = false;
        entity.Processing = false;
        entity.LockedUntilUtc = null;
        entity.Status = "Compensated";
        entity.LastError = Truncate(exception.Message, 2048);
        entity.NextAttemptAtUtc = null;
    }

    internal static void MarkRejected(InspectionIngestOutboxEntity entity, Exception exception)
    {
        entity.Completed = false;
        entity.Processing = false;
        entity.LockedUntilUtc = null;
        entity.Status = "Rejected";
        entity.LastError = Truncate(exception.Message, 2048);
        entity.NextAttemptAtUtc = null;
    }

    internal static void MarkLeaseAcquired(
        InspectionIngestOutboxEntity entity,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration)
    {
        entity.Processing = true;
        entity.LockedUntilUtc = nowUtc.Add(leaseDuration);
        entity.LastAttemptAtUtc = nowUtc;
        entity.Status = "Processing";
    }

    internal static void MarkMetadataWritten(InspectionIngestOutboxEntity entity, DateTimeOffset nowUtc)
    {
        entity.MetadataWritten = true;
        entity.Status = "MetadataWritten";
        entity.LastError = string.Empty;
        entity.NextAttemptAtUtc = nowUtc;
    }

    internal static void MarkQueueMessageSent(InspectionIngestOutboxEntity entity)
    {
        entity.QueueMessageSent = true;
    }

    internal static void MarkCompleted(InspectionIngestOutboxEntity entity)
    {
        entity.Completed = true;
        entity.Processing = false;
        entity.LockedUntilUtc = null;
        entity.Status = "Completed";
        entity.LastError = string.Empty;
        entity.NextAttemptAtUtc = null;
    }

    internal static void MarkRetryScheduled(InspectionIngestOutboxEntity entity, Exception exception, DateTimeOffset nowUtc)
    {
        entity.Completed = false;
        entity.Processing = false;
        entity.LockedUntilUtc = null;
        entity.RetryCount++;
        entity.Status = "PendingRetry";
        entity.LastError = Truncate(exception.Message, 2048);
        entity.NextAttemptAtUtc = nowUtc.Add(GetRetryDelay(entity.RetryCount));
    }

    internal static ReceiveInspectionResponse BuildResponse(ReceiveInspectionRequest request)
    {
        return new ReceiveInspectionResponse(
            "Received",
            request.SessionId ?? string.Empty,
            request.Name ?? string.Empty,
            request.QueryParams ?? new Dictionary<string, string>(),
            "Accepted for eventual processing");
    }

    internal static TimeSpan GetRetryDelay(int retryCount)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Min(retryCount, 8)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool QueryParamsMatch(
        IReadOnlyDictionary<string, string> existingQueryParams,
        IReadOnlyDictionary<string, string> incomingQueryParams)
    {
        if (existingQueryParams.Count != incomingQueryParams.Count)
        {
            return false;
        }

        foreach (var pair in existingQueryParams)
        {
            if (!incomingQueryParams.TryGetValue(pair.Key, out var incomingValue) ||
                !string.Equals(pair.Value, incomingValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool FilesMatch(
        IReadOnlyList<InspectionIngestFileManifest> existingFiles,
        IReadOnlyList<InspectionIngestFileManifest> incomingFiles)
    {
        if (existingFiles.Count != incomingFiles.Count)
        {
            return false;
        }

        for (var i = 0; i < existingFiles.Count; i++)
        {
            var existing = existingFiles[i];
            var incoming = incomingFiles[i];
            if (!string.Equals(existing.FileName, incoming.FileName, StringComparison.Ordinal) ||
                !string.Equals(existing.BlobName, incoming.BlobName, StringComparison.Ordinal) ||
                !string.Equals(existing.FileType, incoming.FileType, StringComparison.Ordinal) ||
                existing.Length != incoming.Length)
            {
                return false;
            }
        }

        return true;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
