using React_Receiver.Domain.Tenants;
using React_Receiver.Models;

namespace React_Receiver.Infrastructure.TenantConfig;

public interface ITenantConfigRepository
{
    bool IsConfigured { get; }
    Task<ResourceEnvelope<TenantConfiguration>?> GetAsync(string tenantId, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken);
    Task<UpsertResult<TenantConfiguration>> UpsertAsync(TenantConfiguration tenantConfiguration, string? expectedETag, CancellationToken cancellationToken);
}
