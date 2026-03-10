using Azure;
using Azure.Data.Tables;
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
}
