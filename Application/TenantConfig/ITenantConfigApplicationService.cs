using React_Receiver.Models;

namespace React_Receiver.Application.TenantConfig;

public interface ITenantConfigApplicationService
{
    Task<ResourceEnvelope<TenantBootstrapResponse>?> GetAsync(string? tenantId, CancellationToken cancellationToken);
    Task<UpsertResult<TenantBootstrapResponse>> UpsertAsync(string tenantId, TenantBootstrapResponse tenantConfig, string? expectedETag, CancellationToken cancellationToken);
    Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken);
}
