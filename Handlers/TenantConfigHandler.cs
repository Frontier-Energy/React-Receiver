using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Handlers;

public interface ITenantConfigHandler
{
    Task<TenantBootstrapResponse?> GetTenantConfigAsync(string? tenantId, CancellationToken cancellationToken);
    Task<TenantBootstrapResponse> UpsertTenantConfigAsync(TenantBootstrapResponse tenantConfig, CancellationToken cancellationToken);
    Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken);
}

public sealed class TenantConfigHandler : ITenantConfigHandler
{
    private const string DefaultTenantId = "qhvac";

    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly Dictionary<string, TenantBootstrapResponse> _tenantConfigs;

    public TenantConfigHandler(
        TableServiceClient tableServiceClient,
        IBootstrapDataProvider bootstrapDataProvider,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
        _tenantConfigs = bootstrapDataProvider
            .GetTenantConfigs()
            .ToDictionary(item => item.Payload.TenantId, item => item.Payload, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<TenantBootstrapResponse?> GetTenantConfigAsync(string? tenantId, CancellationToken cancellationToken)
    {
        var normalizedTenantId = NormalizeTenantId(tenantId);

        if (!HasTableStorageConfiguration())
        {
            return _tenantConfigs.TryGetValue(normalizedTenantId, out var config) ? config : null;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TenantConfigTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        try
        {
            var response = await tableClient.GetEntityAsync<TenantConfigEntity>(
                TenantConfigEntity.PartitionKeyValue,
                normalizedTenantId,
                cancellationToken: cancellationToken);

            return response.Value.ToResponse();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<TenantBootstrapResponse> UpsertTenantConfigAsync(
        TenantBootstrapResponse tenantConfig,
        CancellationToken cancellationToken)
    {
        var normalizedConfig = Normalize(tenantConfig);

        if (!HasTableStorageConfiguration())
        {
            _tenantConfigs[normalizedConfig.TenantId] = normalizedConfig;
            return normalizedConfig;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TenantConfigTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await tableClient.UpsertEntityAsync(
            TenantConfigEntity.FromResponse(normalizedConfig),
            TableUpdateMode.Replace,
            cancellationToken);

        return normalizedConfig;
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!HasTableStorageConfiguration())
        {
            return;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TenantConfigTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        foreach (var tenantConfig in _tenantConfigs)
        {
            if (!overwriteExisting && await TenantConfigExistsAsync(tableClient, tenantConfig.Key, cancellationToken))
            {
                continue;
            }

            await tableClient.UpsertEntityAsync(
                TenantConfigEntity.FromResponse(tenantConfig.Value),
                TableUpdateMode.Replace,
                cancellationToken);
        }
    }

    private static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId.Trim();
    }

    private static TenantBootstrapResponse Normalize(TenantBootstrapResponse config)
    {
        return new TenantBootstrapResponse(
            config.TenantId.Trim(),
            config.DisplayName.Trim(),
            config.UiDefaults,
            config.EnabledForms,
            config.LoginRequired);
    }

    private bool HasTableStorageConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
               !string.IsNullOrWhiteSpace(_tableOptions.TenantConfigTableName);
    }

    private static async Task<bool> TenantConfigExistsAsync(
        TableClient tableClient,
        string tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            await tableClient.GetEntityAsync<TenantConfigEntity>(
                TenantConfigEntity.PartitionKeyValue,
                tenantId,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
