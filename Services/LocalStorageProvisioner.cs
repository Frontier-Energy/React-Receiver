using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Options;

namespace React_Receiver.Services;

public sealed class LocalStorageProvisioner
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly QueueServiceClient _queueServiceClient;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobStorageOptions _blobOptions;
    private readonly QueueStorageOptions _queueOptions;
    private readonly TableStorageOptions _tableOptions;

    public LocalStorageProvisioner(
        BlobServiceClient blobServiceClient,
        QueueServiceClient queueServiceClient,
        TableServiceClient tableServiceClient,
        IOptions<BlobStorageOptions> blobOptions,
        IOptions<QueueStorageOptions> queueOptions,
        IOptions<TableStorageOptions> tableOptions)
    {
        _blobServiceClient = blobServiceClient;
        _queueServiceClient = queueServiceClient;
        _tableServiceClient = tableServiceClient;
        _blobOptions = blobOptions.Value;
        _queueOptions = queueOptions.Value;
        _tableOptions = tableOptions.Value;
    }

    public async Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        foreach (var containerName in BlobStorageHealthCheck.GetRequiredContainerNames(_blobOptions))
        {
            await _blobServiceClient.GetBlobContainerClient(containerName).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        foreach (var queueName in QueueStorageHealthCheck.GetRequiredQueueNames(_queueOptions))
        {
            await _queueServiceClient.GetQueueClient(queueName).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }

        foreach (var tableName in TableStorageHealthCheck.RequiredSettingNames(_tableOptions).Values
                     .Where(static value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Cast<string>())
        {
            await _tableServiceClient.CreateTableIfNotExistsAsync(tableName, cancellationToken);
        }
    }
}
