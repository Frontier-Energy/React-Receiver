using React_Receiver.Domain.Tenants;

namespace React_Receiver.Infrastructure.TenantConfig;

public interface ITenantConfigRepository
{
    bool IsConfigured { get; }
    Task<TenantConfiguration?> GetAsync(string tenantId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken);
    Task<TenantConfiguration> UpsertAsync(TenantConfiguration tenantConfiguration, CancellationToken cancellationToken);
}
