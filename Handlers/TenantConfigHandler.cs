using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;
using React_Receiver.Services;
using System.Collections.Concurrent;

namespace React_Receiver.Handlers;

public interface ITenantConfigHandler
{
    Task<TenantBootstrapResponse?> GetTenantConfigAsync(string? tenantId, CancellationToken cancellationToken);
    Task<TenantBootstrapResponse> UpsertTenantConfigAsync(TenantBootstrapResponse tenantConfig, CancellationToken cancellationToken);
}

public sealed class TenantConfigHandler : ITenantConfigHandler
{
    private const string DefaultTenantId = "qhvac";
    private static readonly ConcurrentDictionary<string, TenantBootstrapResponse> TenantConfigs =
        new(
            new Dictionary<string, TenantBootstrapResponse>(StringComparer.OrdinalIgnoreCase)
            {
                ["qhvac"] = BuildDefaultConfig(),
                ["lire"] = new TenantBootstrapResponse(
                    TenantId: "lire",
                    DisplayName: "LIRE",
                    UiDefaults: new UiDefaults(
                        Theme: "mist",
                        Font: "\"Source Sans Pro\", \"Helvetica Neue\", Arial, sans-serif",
                        Language: "en",
                        ShowLeftFlyout: false,
                        ShowRightFlyout: true,
                        ShowInspectionStatsButton: false),
                    EnabledForms: Array.Empty<string>(),
                    LoginRequired: false)
            });
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public TenantConfigHandler(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
    }

    public async Task<TenantBootstrapResponse?> GetTenantConfigAsync(string? tenantId, CancellationToken cancellationToken)
    {
        var normalizedTenantId = NormalizeTenantId(tenantId);

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.TenantConfigTableName))
        {
            return TenantConfigs.TryGetValue(normalizedTenantId, out var config) ? config : null;
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
            if (!TenantConfigs.TryGetValue(normalizedTenantId, out var defaultConfig))
            {
                return null;
            }

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
            TenantConfigs[normalizedConfig.TenantId] = normalizedConfig;
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

    private static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? DefaultTenantId : tenantId.Trim();
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
