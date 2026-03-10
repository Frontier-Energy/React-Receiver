using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Inspections;

public sealed class AzureInspectionRepository : IInspectionRepository
{
    private static readonly TimeSpan ProcessingLeaseDuration = TimeSpan.FromMinutes(2);
    private readonly AzureInspectionArtifactStore _artifactStore;
    private readonly AzureInspectionFinalizer _finalizer;
    private readonly AzureInspectionOutboxStore _outboxStore;

    public AzureInspectionRepository(
        BlobServiceClient blobServiceClient,
        QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<BlobStorageOptions> blobOptions,
        Microsoft.Extensions.Options.IOptions<QueueStorageOptions> queueOptions,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions,
        IStorageOperationObserver storageObserver,
        IInspectionFileSecurityInspector fileSecurityInspector)
    {
        _artifactStore = new AzureInspectionArtifactStore(
            blobServiceClient,
            tableServiceClient,
            blobOptions.Value,
            tableOptions.Value,
            storageObserver,
            fileSecurityInspector);
        _finalizer = new AzureInspectionFinalizer(
            queueServiceClient,
            tableServiceClient,
            queueOptions.Value,
            tableOptions.Value,
            storageObserver);
        _outboxStore = new AzureInspectionOutboxStore(
            tableServiceClient,
            tableOptions.Value,
            storageObserver);
    }

    public async Task<ReceiveInspectionResponse> PrepareAsync(
        ReceiveInspectionRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedRequest = InspectionIngestStateMachine.NormalizeRequest(request);
        var manifest = InspectionIngestStateMachine.BuildManifest(normalizedRequest);
        var outboxEntity = await _outboxStore.CreateOrLoadAsync(normalizedRequest, manifest, cancellationToken);

        if (InspectionIngestStateMachine.IsPreparationComplete(outboxEntity))
        {
            return InspectionIngestStateMachine.BuildResponse(normalizedRequest);
        }

        try
        {
            if (!outboxEntity.PayloadStaged)
            {
                await _artifactStore.SavePayloadAsync(normalizedRequest, cancellationToken);
                InspectionIngestStateMachine.MarkPayloadStaged(outboxEntity, DateTimeOffset.UtcNow);
                await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            }

            if (!outboxEntity.FilesStaged)
            {
                await _artifactStore.SaveFilesAsync(normalizedRequest, manifest, cancellationToken);
                InspectionIngestStateMachine.MarkFilesStaged(outboxEntity, DateTimeOffset.UtcNow);
                await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            }
        }
        catch (InspectionFileSecurityException ex)
        {
            await _artifactStore.CompensateStagingAsync(
                normalizedRequest.SessionId ?? string.Empty,
                manifest,
                deleteQuarantineFiles: false,
                cancellationToken);
            InspectionIngestStateMachine.MarkRejected(outboxEntity, ex);
            await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            await _artifactStore.CompensateStagingAsync(
                normalizedRequest.SessionId ?? string.Empty,
                manifest,
                deleteQuarantineFiles: true,
                cancellationToken);
            InspectionIngestStateMachine.MarkCompensated(outboxEntity, ex);
            await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            throw;
        }

        return InspectionIngestStateMachine.BuildResponse(normalizedRequest);
    }

    public async Task<bool> ProcessPendingAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var outboxEntity = await _outboxStore.TryAcquireLeaseAsync(sessionId, ProcessingLeaseDuration, cancellationToken);
        if (outboxEntity is null)
        {
            return false;
        }

        try
        {
            if (!outboxEntity.FilesVerified)
            {
                await _artifactStore.VerifyAndPromoteFilesAsync(outboxEntity, cancellationToken);
                InspectionIngestStateMachine.MarkFilesVerified(outboxEntity, DateTimeOffset.UtcNow);
                await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            }

            if (!outboxEntity.MetadataWritten)
            {
                await _finalizer.SaveInspectionFilesMetadataAsync(outboxEntity, cancellationToken);
                InspectionIngestStateMachine.MarkMetadataWritten(outboxEntity, DateTimeOffset.UtcNow);
                await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            }

            if (!outboxEntity.QueueMessageSent)
            {
                await _finalizer.SendQueueMessageAsync(outboxEntity.SessionId, cancellationToken);
                InspectionIngestStateMachine.MarkQueueMessageSent(outboxEntity);
            }

            InspectionIngestStateMachine.MarkCompleted(outboxEntity);
            await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            return true;
        }
        catch (InspectionFileSecurityException ex)
        {
            InspectionIngestStateMachine.MarkRejected(outboxEntity, ex);
            await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            InspectionIngestStateMachine.MarkRetryScheduled(outboxEntity, ex, DateTimeOffset.UtcNow);
            await _outboxStore.SaveAsync(outboxEntity, cancellationToken);
            return false;
        }
    }

    public Task<IReadOnlyCollection<string>> GetPendingSessionIdsAsync(
        int maxResults,
        CancellationToken cancellationToken)
    {
        return _outboxStore.GetPendingSessionIdsAsync(maxResults, cancellationToken);
    }

    public Task<GetInspectionResponse?> GetAsync(string sessionId, CancellationToken cancellationToken)
    {
        return _artifactStore.GetInspectionAsync(sessionId, cancellationToken);
    }

    public Task<InspectionFileStreamResult?> GetFileAsync(
        string sessionId,
        string fileName,
        CancellationToken cancellationToken)
    {
        return _artifactStore.GetFileAsync(sessionId, fileName, cancellationToken);
    }
}
