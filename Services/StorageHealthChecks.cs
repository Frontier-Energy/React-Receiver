using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace React_Receiver.Services;

public sealed class StorageConfigurationHealthCheck : IHealthCheck
{
    private readonly BlobStorageOptions _blobOptions;
    private readonly QueueStorageOptions _queueOptions;
    private readonly TableStorageOptions _tableOptions;

    public StorageConfigurationHealthCheck(
        IOptions<BlobStorageOptions> blobOptions,
        IOptions<QueueStorageOptions> queueOptions,
        IOptions<TableStorageOptions> tableOptions)
    {
        _blobOptions = blobOptions.Value;
        _queueOptions = queueOptions.Value;
        _tableOptions = tableOptions.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(_blobOptions.ConnectionString) &&
            string.IsNullOrWhiteSpace(_blobOptions.ServiceUri))
        {
            problems.Add("BlobStorage:ConnectionString|ServiceUri");
        }

        if (string.IsNullOrWhiteSpace(_blobOptions.ContainerName))
        {
            problems.Add("BlobStorage:ContainerName");
        }

        if (string.IsNullOrWhiteSpace(_queueOptions.ConnectionString) &&
            string.IsNullOrWhiteSpace(_queueOptions.ServiceUri))
        {
            problems.Add("QueueStorage:ConnectionString|ServiceUri");
        }

        if (string.IsNullOrWhiteSpace(_queueOptions.QueueName))
        {
            problems.Add("QueueStorage:QueueName");
        }

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
            string.IsNullOrWhiteSpace(_tableOptions.ServiceUri))
        {
            problems.Add("TableStorage:ConnectionString|ServiceUri");
        }

        foreach (var tableName in TableStorageHealthCheck.RequiredSettingNames(_tableOptions))
        {
            if (string.IsNullOrWhiteSpace(tableName.Value))
            {
                problems.Add(tableName.Key);
            }
        }

        if (problems.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Required storage configuration is missing.",
                data: new Dictionary<string, object> { ["missing"] = problems }));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Storage configuration is present."));
    }
}

public sealed class BlobStorageHealthCheck : IHealthCheck
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _options;

    public BlobStorageHealthCheck(
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageOptions> options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        foreach (var containerName in GetRequiredContainerNames(_options))
        {
            try
            {
                var exists = await _blobServiceClient
                    .GetBlobContainerClient(containerName)
                    .ExistsAsync(cancellationToken);

                if (!exists.Value)
                {
                    failures.Add($"Container '{containerName}' does not exist.");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Container '{containerName}' check failed: {ex.Message}");
            }
        }

        return failures.Count > 0
            ? HealthCheckResult.Unhealthy("Blob storage dependency check failed.", data: new Dictionary<string, object> { ["errors"] = failures })
            : HealthCheckResult.Healthy("Blob storage dependencies are reachable.");
    }

    public static IReadOnlyCollection<string> GetRequiredContainerNames(BlobStorageOptions options)
    {
        return new[]
        {
            options.ContainerName?.Trim(),
            StorageDependencyNames.FilesQuarantineContainerName,
            StorageDependencyNames.FilesContainerName
        }
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Cast<string>()
        .ToArray();
    }
}

public sealed class TableStorageHealthCheck : IHealthCheck
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _options;

    public TableStorageHealthCheck(
        TableServiceClient tableServiceClient,
        IOptions<TableStorageOptions> options)
    {
        _tableServiceClient = tableServiceClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        foreach (var tableName in RequiredSettingNames(_options).Values.Where(static value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                await foreach (var _ in tableClient.QueryAsync<TableEntity>(maxPerPage: 1, cancellationToken: cancellationToken))
                {
                    break;
                }
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                failures.Add($"Table '{tableName}' does not exist.");
            }
            catch (Exception ex)
            {
                failures.Add($"Table '{tableName}' check failed: {ex.Message}");
            }
        }

        return failures.Count > 0
            ? HealthCheckResult.Unhealthy("Table storage dependency check failed.", data: new Dictionary<string, object> { ["errors"] = failures })
            : HealthCheckResult.Healthy("Table storage dependencies are reachable.");
    }

    public static IReadOnlyDictionary<string, string?> RequiredSettingNames(TableStorageOptions options)
    {
        return new Dictionary<string, string?>
        {
            ["TableStorage:TableName"] = options.TableName?.Trim(),
            ["TableStorage:InspectionFilesTableName"] = options.InspectionFilesTableName?.Trim(),
            ["TableStorage:InspectionIngestOutboxTableName"] = options.InspectionIngestOutboxTableName?.Trim(),
            ["TableStorage:TenantConfigTableName"] = options.TenantConfigTableName?.Trim(),
            ["TableStorage:MeTableName"] = options.MeTableName?.Trim(),
            ["TableStorage:FormSchemaCatalogTableName"] = options.FormSchemaCatalogTableName?.Trim(),
            ["TableStorage:FormSchemasTableName"] = options.FormSchemasTableName?.Trim(),
            ["TableStorage:TranslationsTableName"] = options.TranslationsTableName?.Trim()
        };
    }
}

public sealed class QueueStorageHealthCheck : IHealthCheck
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly QueueStorageOptions _options;

    public QueueStorageHealthCheck(
        QueueServiceClient queueServiceClient,
        IOptions<QueueStorageOptions> options)
    {
        _queueServiceClient = queueServiceClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();

        foreach (var queueName in GetRequiredQueueNames(_options))
        {
            try
            {
                var exists = await _queueServiceClient
                    .GetQueueClient(queueName)
                    .ExistsAsync(cancellationToken);

                if (!exists.Value)
                {
                    failures.Add($"Queue '{queueName}' does not exist.");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Queue '{queueName}' check failed: {ex.Message}");
            }
        }

        return failures.Count > 0
            ? HealthCheckResult.Unhealthy("Queue storage dependency check failed.", data: new Dictionary<string, object> { ["errors"] = failures })
            : HealthCheckResult.Healthy("Queue storage dependencies are reachable.");
    }

    public static IReadOnlyCollection<string> GetRequiredQueueNames(QueueStorageOptions options)
    {
        return new[]
        {
            options.QueueName?.Trim()
        }
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Cast<string>()
        .ToArray();
    }
}
