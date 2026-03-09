using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using React_Receiver.Observability;

namespace React_Receiver.Services;

public sealed class StorageInfrastructureHostedService : IHostedService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly QueueStorageOptions _queueOptions;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;

    public StorageInfrastructureHostedService(
        BlobServiceClient blobServiceClient,
        QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient,
        IOptions<BlobStorageOptions> blobOptions,
        IOptions<QueueStorageOptions> queueOptions,
        IOptions<TableStorageOptions> tableOptions,
        IStorageOperationObserver storageObserver)
    {
        _blobServiceClient = blobServiceClient;
        _queueServiceClient = queueServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions.Value;
        _queueOptions = queueOptions.Value;
        _tableOptions = tableOptions.Value;
        _storageObserver = storageObserver;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureBlobContainerAsync(_blobOptions.ContainerName, "EnsurePrimaryBlobContainer", cancellationToken);
        await EnsureBlobContainerAsync(StorageDependencyNames.FilesContainerName, "EnsureInspectionFilesContainer", cancellationToken);
        await EnsureQueueAsync(_queueOptions.QueueName, "EnsureInspectionQueue", cancellationToken);

        await EnsureTableAsync(_tableOptions.TableName, "EnsureUsersTable", cancellationToken);
        await EnsureTableAsync(_tableOptions.MeTableName, "EnsureCurrentUserTable", cancellationToken);
        await EnsureTableAsync(_tableOptions.InspectionFilesTableName, "EnsureInspectionFilesTable", cancellationToken);
        await EnsureTableAsync(_tableOptions.InspectionIngestOutboxTableName, "EnsureInspectionIngestOutboxTable", cancellationToken);
        await EnsureTableAsync(_tableOptions.TenantConfigTableName, "EnsureTenantConfigTable", cancellationToken);
        await EnsureTableAsync(_tableOptions.FormSchemaCatalogTableName, "EnsureFormSchemaCatalogTable", cancellationToken);
        await EnsureTableAsync(_tableOptions.FormSchemasTableName, "EnsureFormSchemasTable", cancellationToken);
        await EnsureTableAsync(_tableOptions.TranslationsTableName, "EnsureTranslationsTable", cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private Task EnsureBlobContainerAsync(string containerName, string operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return Task.CompletedTask;
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        return _storageObserver.ExecuteAsync(
            "blob",
            containerName,
            operation,
            ct => containerClient.CreateIfNotExistsAsync(cancellationToken: ct),
            cancellationToken);
    }

    private Task EnsureQueueAsync(string queueName, string operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(queueName))
        {
            return Task.CompletedTask;
        }

        var queueClient = _queueServiceClient.GetQueueClient(queueName);
        return _storageObserver.ExecuteAsync(
            "queue",
            queueName,
            operation,
            ct => queueClient.CreateIfNotExistsAsync(cancellationToken: ct),
            cancellationToken);
    }

    private Task EnsureTableAsync(string tableName, string operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return Task.CompletedTask;
        }

        var tableClient = _tableServiceClient.GetTableClient(tableName);
        return _storageObserver.ExecuteAsync(
            "table",
            tableName,
            operation,
            ct => tableClient.CreateIfNotExistsAsync(cancellationToken: ct),
            cancellationToken);
    }
}
