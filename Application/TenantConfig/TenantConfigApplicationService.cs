using React_Receiver.Domain.Tenants;
using React_Receiver.Infrastructure.TenantConfig;
using React_Receiver.Models;

namespace React_Receiver.Application.TenantConfig;

public sealed class TenantConfigApplicationService : ITenantConfigApplicationService
{
    private readonly ITenantConfigRepository _repository;
    private readonly ITenantConfigSeedStore _seedStore;

    public TenantConfigApplicationService(
        ITenantConfigRepository repository,
        ITenantConfigSeedStore seedStore)
    {
        _repository = repository;
        _seedStore = seedStore;
    }

    public async Task<ResourceEnvelope<TenantBootstrapResponse>?> GetAsync(string? tenantId, CancellationToken cancellationToken)
    {
        var normalizedTenantId = TenantConfiguration.NormalizeTenantId(tenantId);

        if (!_repository.IsConfigured)
        {
            return _seedStore.Get(normalizedTenantId);
        }

        var configuration = await _repository.GetAsync(normalizedTenantId, cancellationToken);
        return configuration is null
            ? null
            : new ResourceEnvelope<TenantBootstrapResponse>(
                Map(configuration.Resource),
                configuration.ETag,
                configuration.Version);
    }

    public async Task<UpsertResult<TenantBootstrapResponse>> UpsertAsync(
        string tenantId,
        TenantBootstrapResponse tenantConfig,
        string? expectedETag,
        CancellationToken cancellationToken)
    {
        var normalized = TenantConfiguration.Normalize(Map(tenantConfig with { TenantId = tenantId }));

        if (!_repository.IsConfigured)
        {
            return _seedStore.Upsert(normalized, expectedETag);
        }

        var saved = await _repository.UpsertAsync(normalized, expectedETag, cancellationToken);
        return new UpsertResult<TenantBootstrapResponse>(Map(saved.Resource), saved.Created, saved.Version, saved.ETag);
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return;
        }

        foreach (var tenantConfig in _seedStore.GetAll())
        {
            if (!overwriteExisting && await _repository.ExistsAsync(tenantConfig.Key, cancellationToken))
            {
                continue;
            }

            await _repository.UpsertAsync(tenantConfig.Value, null, cancellationToken);
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
