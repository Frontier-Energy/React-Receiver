using System.Collections.Concurrent;
using React_Receiver.Domain.Tenants;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.TenantConfig;

public interface ITenantConfigSeedStore
{
    TenantBootstrapResponse? Get(string tenantId);
    UpsertResult<TenantBootstrapResponse> Upsert(TenantConfiguration configuration);
    IReadOnlyCollection<KeyValuePair<string, TenantConfiguration>> GetAll();
}

public sealed class TenantConfigSeedStore : ITenantConfigSeedStore
{
    private readonly ConcurrentDictionary<string, TenantConfiguration> _tenantConfigs;

    public TenantConfigSeedStore(IBootstrapDataProvider bootstrapDataProvider)
    {
        _tenantConfigs = new ConcurrentDictionary<string, TenantConfiguration>(
            bootstrapDataProvider
                .GetTenantConfigs()
                .Select(item => Map(item.Payload))
                .Select(item => new KeyValuePair<string, TenantConfiguration>(item.TenantId, item)),
            StringComparer.OrdinalIgnoreCase);
    }

    public TenantBootstrapResponse? Get(string tenantId)
    {
        return _tenantConfigs.TryGetValue(tenantId, out var config) ? Map(config) : null;
    }

    public UpsertResult<TenantBootstrapResponse> Upsert(TenantConfiguration configuration)
    {
        var created = !_tenantConfigs.ContainsKey(configuration.TenantId);
        _tenantConfigs[configuration.TenantId] = configuration;
        return new UpsertResult<TenantBootstrapResponse>(Map(configuration), created);
    }

    public IReadOnlyCollection<KeyValuePair<string, TenantConfiguration>> GetAll()
    {
        return _tenantConfigs.ToArray();
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
