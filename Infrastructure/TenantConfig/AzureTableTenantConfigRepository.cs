using Azure;
using Azure.Data.Tables;
using React_Receiver.Domain.Tenants;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.TenantConfig;

public sealed class AzureTableTenantConfigRepository : ITenantConfigRepository
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;

    public AzureTableTenantConfigRepository(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions,
        IStorageOperationObserver storageObserver)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
        _storageObserver = storageObserver;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
        !string.IsNullOrWhiteSpace(_tableOptions.TenantConfigTableName);

    public async Task<TenantConfiguration?> GetAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TenantConfigTableName);
        try
        {
            var response = await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TenantConfigTableName,
                "GetTenantConfig",
                ct => tableClient.GetEntityAsync<TenantConfigEntity>(
                    TenantConfigEntity.PartitionKeyValue,
                    tenantId,
                    cancellationToken: ct),
                cancellationToken);
            return Map(response.Value.ToResponse());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TenantConfigTableName);
        try
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TenantConfigTableName,
                "TenantConfigExists",
                ct => tableClient.GetEntityAsync<TenantConfigEntity>(
                    TenantConfigEntity.PartitionKeyValue,
                    tenantId,
                    cancellationToken: ct),
                cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<TenantConfiguration> UpsertAsync(
        TenantConfiguration tenantConfiguration,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TenantConfigTableName);
        await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.TenantConfigTableName,
            "UpsertTenantConfig",
            ct => tableClient.UpsertEntityAsync(
                TenantConfigEntity.FromResponse(Map(tenantConfiguration)),
                TableUpdateMode.Replace,
                ct),
            cancellationToken);
        return tenantConfiguration;
    }

    private static TenantConfiguration Map(TenantBootstrapResponse response)
    {
        return new TenantConfiguration(
            response.TenantId ?? string.Empty,
            response.DisplayName ?? string.Empty,
            new TenantUiDefaults(
                response.UiDefaults?.Theme ?? string.Empty,
                response.UiDefaults?.Font ?? string.Empty,
                response.UiDefaults?.Language ?? string.Empty,
                response.UiDefaults?.ShowLeftFlyout ?? false,
                response.UiDefaults?.ShowRightFlyout ?? false,
                response.UiDefaults?.ShowInspectionStatsButton ?? false),
            response.EnabledForms ?? Array.Empty<string>(),
            response.LoginRequired);
    }

    private static TenantBootstrapResponse Map(TenantConfiguration configuration)
    {
        return new TenantBootstrapResponse(
            configuration.TenantId,
            configuration.DisplayName,
            new UiDefaults(
                configuration.UiDefaults.Theme,
                configuration.UiDefaults.Font,
                configuration.UiDefaults.Language,
                configuration.UiDefaults.ShowLeftFlyout,
                configuration.UiDefaults.ShowRightFlyout,
                configuration.UiDefaults.ShowInspectionStatsButton),
            configuration.EnabledForms,
            configuration.LoginRequired);
    }
}
