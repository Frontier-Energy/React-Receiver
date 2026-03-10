using Azure;
using Azure.Data.Tables;
using System.Text.Json;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Inspections;

internal sealed class AzureInspectionOutboxStore
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;

    internal AzureInspectionOutboxStore(
        TableServiceClient tableServiceClient,
        TableStorageOptions tableOptions,
        IStorageOperationObserver storageObserver)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions;
        _storageObserver = storageObserver;
    }

    internal async Task<InspectionIngestOutboxEntity> CreateOrLoadAsync(
        ReceiveInspectionRequest request,
        InspectionIngestFileManifest[] manifest,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var entity = InspectionIngestStateMachine.CreateOutboxEntity(request, manifest, DateTimeOffset.UtcNow);

        try
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.InspectionIngestOutboxTableName,
                "AddInspectionIngestOutbox",
                ct => tableClient.AddEntityAsync(entity, ct),
                cancellationToken);
            return entity;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            var existing = await GetAsync(request.SessionId ?? string.Empty, cancellationToken);
            InspectionIngestStateMachine.ValidateEquivalentRequest(existing, request, manifest);
            return existing;
        }
    }

    internal async Task<InspectionIngestOutboxEntity?> TryAcquireLeaseAsync(
        string sessionId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var entity = await TryGetAsync(sessionId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (!InspectionIngestStateMachine.IsReadyToAcquireLease(entity, now))
        {
            return null;
        }

        var leasedEntity = entity!;
        InspectionIngestStateMachine.MarkLeaseAcquired(leasedEntity, now, leaseDuration);

        try
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.InspectionIngestOutboxTableName,
                "AcquireInspectionIngestLease",
                ct => tableClient.UpdateEntityAsync(leasedEntity, leasedEntity.ETag, TableUpdateMode.Replace, ct),
                cancellationToken);
            return leasedEntity;
        }
        catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409)
        {
            return null;
        }
    }

    internal async Task<IReadOnlyCollection<string>> GetPendingSessionIdsAsync(
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (maxResults <= 0)
        {
            return Array.Empty<string>();
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var now = DateTimeOffset.UtcNow;
        var sessions = new List<string>(maxResults);

        await foreach (var entity in tableClient.QueryAsync<InspectionIngestOutboxEntity>(
                           item => item.PartitionKey == InspectionIngestOutboxEntity.PartitionKeyValue,
                           maxPerPage: maxResults,
                           cancellationToken: cancellationToken))
        {
            if (!InspectionIngestStateMachine.IsPending(entity, now))
            {
                continue;
            }

            sessions.Add(entity.SessionId);
            if (sessions.Count >= maxResults)
            {
                break;
            }
        }

        return sessions;
    }

    internal async Task<IReadOnlyCollection<InspectionIngestOutboxSessionSummary>> GetSessionsAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return Array.Empty<InspectionIngestOutboxSessionSummary>();
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var sessions = new List<InspectionIngestOutboxSessionSummary>();

        await foreach (var entity in tableClient.QueryAsync<InspectionIngestOutboxEntity>(
                           item => item.PartitionKey == InspectionIngestOutboxEntity.PartitionKeyValue,
                           cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(status) &&
                !string.Equals(entity.Status, status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            sessions.Add(ToSummary(entity));
        }

        return sessions
            .OrderByDescending(item => item.NextAttemptAtUtc ?? item.LastAttemptAtUtc ?? item.UpdatedAtUtc)
            .Take(limit)
            .ToArray();
    }

    internal async Task<InspectionIngestOutboxSessionDetail?> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var entity = await TryGetAsync(sessionId, cancellationToken);
        return entity is null ? null : ToDetail(entity);
    }

    internal async Task<ReplayInspectionIngestSessionResponse> ReplayAsync(
        string sessionId,
        bool force,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var entity = await TryGetAsync(sessionId, cancellationToken);
            if (entity is null)
            {
                return new ReplayInspectionIngestSessionResponse(false, "Session not found.", null);
            }

            if (!InspectionIngestStateMachine.CanReplay(entity))
            {
                return new ReplayInspectionIngestSessionResponse(
                    false,
                    "Session cannot be replayed because required staged artifacts are not available.",
                    ToDetail(entity));
            }

            if (entity.Completed)
            {
                return new ReplayInspectionIngestSessionResponse(false, "Completed sessions cannot be replayed.", ToDetail(entity));
            }

            if (entity.Processing && !force &&
                entity.LockedUntilUtc is not null &&
                entity.LockedUntilUtc > DateTimeOffset.UtcNow)
            {
                return new ReplayInspectionIngestSessionResponse(
                    false,
                    "Session is currently leased for processing. Use force to break the lease.",
                    ToDetail(entity));
            }

            if (entity.TerminalFailure && !force)
            {
                return new ReplayInspectionIngestSessionResponse(
                    false,
                    "Terminal sessions require force to be replayed.",
                    ToDetail(entity));
            }

            InspectionIngestStateMachine.MarkReplayQueued(entity, DateTimeOffset.UtcNow, force);

            try
            {
                await _storageObserver.ExecuteAsync(
                    "table",
                    _tableOptions.InspectionIngestOutboxTableName,
                    "ReplayInspectionIngestOutbox",
                    ct => tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct),
                    cancellationToken);

                return new ReplayInspectionIngestSessionResponse(true, "Replay queued.", ToDetail(entity));
            }
            catch (RequestFailedException ex) when (ex.Status == 412 || ex.Status == 409)
            {
            }
        }

        return new ReplayInspectionIngestSessionResponse(false, "Session changed while replaying. Retry the request.", null);
    }

    internal Task SaveAsync(InspectionIngestOutboxEntity entity, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        return _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.InspectionIngestOutboxTableName,
            "UpsertInspectionIngestOutbox",
            ct => tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct),
            cancellationToken);
    }

    private async Task<InspectionIngestOutboxEntity?> TryGetAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            return await GetAsync(sessionId, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private async Task<InspectionIngestOutboxEntity> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionIngestOutboxTableName);
        var response = await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.InspectionIngestOutboxTableName,
            "GetInspectionIngestOutbox",
            ct => tableClient.GetEntityAsync<InspectionIngestOutboxEntity>(
                InspectionIngestOutboxEntity.PartitionKeyValue,
                sessionId,
                cancellationToken: ct),
            cancellationToken);
        return response.Value;
    }

    private static InspectionIngestOutboxSessionSummary ToSummary(InspectionIngestOutboxEntity entity)
    {
        return new InspectionIngestOutboxSessionSummary(
            entity.SessionId,
            entity.UserId,
            entity.Name,
            entity.Status,
            entity.Completed,
            entity.TerminalFailure,
            InspectionIngestStateMachine.CanReplay(entity),
            entity.RetryCount,
            entity.LastError,
            entity.LastAttemptAtUtc,
            entity.NextAttemptAtUtc,
            entity.LockedUntilUtc,
            entity.PoisonedAtUtc,
            entity.Timestamp);
    }

    private static InspectionIngestOutboxSessionDetail ToDetail(InspectionIngestOutboxEntity entity)
    {
        var queryParams = string.IsNullOrWhiteSpace(entity.QueryParamsJson)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(
                entity.QueryParamsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Dictionary<string, string>();

        return new InspectionIngestOutboxSessionDetail(
            entity.SessionId,
            entity.UserId,
            entity.Name,
            queryParams,
            InspectionIngestStateMachine.DeserializeManifest(entity.FilesJson),
            entity.Status,
            entity.PayloadStaged,
            entity.FilesStaged,
            entity.FilesVerified,
            entity.MetadataWritten,
            entity.QueueMessageSent,
            entity.Completed,
            entity.TerminalFailure,
            entity.Processing,
            InspectionIngestStateMachine.CanReplay(entity),
            entity.RetryCount,
            entity.LastError,
            entity.LastAttemptAtUtc,
            entity.NextAttemptAtUtc,
            entity.LockedUntilUtc,
            entity.PoisonedAtUtc,
            entity.Timestamp);
    }
}
