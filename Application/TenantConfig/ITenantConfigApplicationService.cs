using React_Receiver.Models;

namespace React_Receiver.Application.TenantConfig;

public interface ITenantConfigApplicationService
{
    Task<TenantBootstrapResponse?> GetAsync(string? tenantId, CancellationToken cancellationToken);
    Task<TenantBootstrapResponse> UpsertAsync(TenantBootstrapResponse tenantConfig, CancellationToken cancellationToken);
    Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken);
}
