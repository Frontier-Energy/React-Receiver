using System.Collections.Concurrent;
using React_Receiver.Application.Concurrency;
using React_Receiver.Domain.Tenants;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.TenantConfig;

public interface ITenantConfigSeedStore
{
    ResourceEnvelope<TenantBootstrapResponse>? Get(string tenantId);
    UpsertResult<TenantBootstrapResponse> Upsert(TenantConfiguration configuration, string? expectedETag);
    IReadOnlyCollection<KeyValuePair<string, TenantConfiguration>> GetAll();
}

public sealed class TenantConfigSeedStore : ITenantConfigSeedStore
{
    private sealed record SeedEntry(string ETag, TenantConfiguration Resource);

    private readonly ConcurrentDictionary<string, SeedEntry> _tenantConfigs;
    private readonly object _gate = new();

    public TenantConfigSeedStore(IBootstrapDataProvider bootstrapDataProvider)
    {
        _tenantConfigs = new ConcurrentDictionary<string, SeedEntry>(
            bootstrapDataProvider
                .GetTenantConfigs()
                .Select(item => Map(item.Payload))
                .Select(item => new KeyValuePair<string, SeedEntry>(
                    item.TenantId,
                    new SeedEntry(CreateETag(item.TenantId), item))),
            StringComparer.OrdinalIgnoreCase);
    }

    public ResourceEnvelope<TenantBootstrapResponse>? Get(string tenantId)
    {
        return _tenantConfigs.TryGetValue(tenantId, out var config)
            ? new ResourceEnvelope<TenantBootstrapResponse>(Map(config.Resource), config.ETag)
            : null;
    }

    public UpsertResult<TenantBootstrapResponse> Upsert(TenantConfiguration configuration, string? expectedETag)
    {
        lock (_gate)
        {
            _tenantConfigs.TryGetValue(configuration.TenantId, out var existing);
            OptimisticConcurrency.EnsureSatisfied(expectedETag, existing?.ETag, $"tenant config '{configuration.TenantId}'");

            var created = existing is null;
            var entry = new SeedEntry(CreateETag(configuration.TenantId), configuration);
            _tenantConfigs[configuration.TenantId] = entry;
            return new UpsertResult<TenantBootstrapResponse>(Map(configuration), created, ETag: entry.ETag);
        }
    }

    public IReadOnlyCollection<KeyValuePair<string, TenantConfiguration>> GetAll()
    {
        return _tenantConfigs
            .Select(item => new KeyValuePair<string, TenantConfiguration>(item.Key, item.Value.Resource))
            .ToArray();
    }

    private static string CreateETag(string tenantId) => $"\"{tenantId}-{Guid.NewGuid():N}\"";

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
