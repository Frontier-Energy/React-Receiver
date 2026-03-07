using React_Receiver.Domain.Tenants;
using React_Receiver.Infrastructure.TenantConfig;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.TenantConfig;

public sealed class TenantConfigApplicationService : ITenantConfigApplicationService
{
    private readonly ITenantConfigRepository _repository;
    private readonly Dictionary<string, TenantConfiguration> _tenantConfigs;

    public TenantConfigApplicationService(
        ITenantConfigRepository repository,
        IBootstrapDataProvider bootstrapDataProvider)
    {
        _repository = repository;
        _tenantConfigs = bootstrapDataProvider
            .GetTenantConfigs()
            .Select(item => Map(item.Payload))
            .ToDictionary(item => item.TenantId, item => item, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<TenantBootstrapResponse?> GetAsync(string? tenantId, CancellationToken cancellationToken)
    {
        var normalizedTenantId = TenantConfiguration.NormalizeTenantId(tenantId);

        if (!_repository.IsConfigured)
        {
            return _tenantConfigs.TryGetValue(normalizedTenantId, out var config) ? Map(config) : null;
        }

        var configuration = await _repository.GetAsync(normalizedTenantId, cancellationToken);
        return configuration is null ? null : Map(configuration);
    }

    public async Task<TenantBootstrapResponse> UpsertAsync(
        TenantBootstrapResponse tenantConfig,
        CancellationToken cancellationToken)
    {
        var normalized = TenantConfiguration.Normalize(Map(tenantConfig));

        if (!_repository.IsConfigured)
        {
            _tenantConfigs[normalized.TenantId] = normalized;
            return Map(normalized);
        }

        var saved = await _repository.UpsertAsync(normalized, cancellationToken);
        return Map(saved);
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return;
        }

        foreach (var tenantConfig in _tenantConfigs)
        {
            if (!overwriteExisting && await _repository.ExistsAsync(tenantConfig.Key, cancellationToken))
            {
                continue;
            }

            await _repository.UpsertAsync(tenantConfig.Value, cancellationToken);
        }
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
