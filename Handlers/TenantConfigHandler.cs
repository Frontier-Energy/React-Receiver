using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Handlers;

public interface ITenantConfigHandler
{
    Task<TenantBootstrapResponse> GetTenantConfigAsync(CancellationToken cancellationToken);
    Task<TenantBootstrapResponse> UpsertTenantConfigAsync(TenantBootstrapResponse tenantConfig, CancellationToken cancellationToken);
}

public sealed class TenantConfigHandler : ITenantConfigHandler
{
    private const string DefaultTenantId = "qhvac";
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public TenantConfigHandler(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
    }

    public async Task<TenantBootstrapResponse> GetTenantConfigAsync(CancellationToken cancellationToken)
    {
        var defaultConfig = BuildDefaultConfig();

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.TenantConfigTableName))
        {
            return defaultConfig;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TenantConfigTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        try
        {
            var response = await tableClient.GetEntityAsync<TenantConfigEntity>(
                TenantConfigEntity.PartitionKeyValue,
                DefaultTenantId,
                cancellationToken: cancellationToken);

            return response.Value.ToResponse();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var entity = TenantConfigEntity.FromResponse(defaultConfig);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
            return defaultConfig;
        }
    }

    public async Task<TenantBootstrapResponse> UpsertTenantConfigAsync(
        TenantBootstrapResponse tenantConfig,
        CancellationToken cancellationToken)
    {
        var normalizedConfig = Normalize(tenantConfig);

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.TenantConfigTableName))
        {
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

    private static TenantBootstrapResponse Normalize(TenantBootstrapResponse config)
    {
        var fallback = BuildDefaultConfig();
        var tenantId = string.IsNullOrWhiteSpace(config.TenantId) ? fallback.TenantId : config.TenantId;
        var displayName = string.IsNullOrWhiteSpace(config.DisplayName) ? fallback.DisplayName : config.DisplayName;
        var uiDefaults = config.UiDefaults ?? fallback.UiDefaults;
        var enabledForms = config.EnabledForms ?? fallback.EnabledForms;

        return new TenantBootstrapResponse(
            TenantId: tenantId,
            DisplayName: displayName,
            UiDefaults: uiDefaults,
            EnabledForms: enabledForms,
            LoginRequired: config.LoginRequired);
    }

    private static TenantBootstrapResponse BuildDefaultConfig()
    {
        return new TenantBootstrapResponse(
            TenantId: DefaultTenantId,
            DisplayName: "QHVAC",
            UiDefaults: new UiDefaults(
                Theme: "harbor",
                Font: "Tahoma, \"Trebuchet MS\", Arial, sans-serif",
                Language: "en",
                ShowLeftFlyout: true,
                ShowRightFlyout: true,
                ShowInspectionStatsButton: false),
            EnabledForms: ["electrical", "electrical-sf", "hvac"],
            LoginRequired: true);
    }
}
