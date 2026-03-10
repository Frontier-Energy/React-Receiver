using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Inspections;

internal sealed class AzureInspectionFinalizer
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly QueueStorageOptions _queueOptions;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;

    internal AzureInspectionFinalizer(
        QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient,
        QueueStorageOptions queueOptions,
        TableStorageOptions tableOptions,
        IStorageOperationObserver storageObserver)
    {
        _queueServiceClient = queueServiceClient;
        _tableServiceClient = tableServiceClient;
        _queueOptions = queueOptions;
        _tableOptions = tableOptions;
        _storageObserver = storageObserver;
    }

    internal async Task SaveInspectionFilesMetadataAsync(
        InspectionIngestOutboxEntity entity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.InspectionFilesTableName))
        {
            return;
        }

        var sessionId = entity.SessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var files = InspectionIngestStateMachine.DeserializeManifest(entity.FilesJson)
            .Select(file => new InspectionFileReference(file.FileName, sessionId, file.FileType))
            .ToArray();

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.InspectionFilesTableName);
        await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.InspectionFilesTableName,
            "UpsertInspectionFileMetadata",
            ct => tableClient.UpsertEntityAsync(
                new InspectionFilesEntity
                {
                    PartitionKey = InspectionFilesEntity.PartitionKeyValue,
                    RowKey = sessionId,
                    SessionId = sessionId,
                    Files = JsonSerializer.Serialize(files)
                },
                TableUpdateMode.Replace,
                ct),
            cancellationToken);
    }

    internal Task SendQueueMessageAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_queueOptions.QueueName))
        {
            return Task.CompletedTask;
        }

        var queueClient = _queueServiceClient.GetQueueClient(_queueOptions.QueueName);
        return _storageObserver.ExecuteAsync(
            "queue",
            _queueOptions.QueueName,
            "SendInspectionQueueMessage",
            ct => queueClient.SendMessageAsync(JsonSerializer.Serialize(new { sessionId }), ct),
            cancellationToken);
    }
}
