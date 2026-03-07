using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.TenantConfig;

public sealed class GetTenantConfigQueryHandler : IRequestHandler<GetTenantConfigQuery, ResourceEnvelope<TenantBootstrapResponse>?>
{
    private readonly ITenantConfigApplicationService _tenantConfigApplicationService;

    public GetTenantConfigQueryHandler(ITenantConfigApplicationService tenantConfigApplicationService)
    {
        _tenantConfigApplicationService = tenantConfigApplicationService;
    }

    public Task<ResourceEnvelope<TenantBootstrapResponse>?> Handle(GetTenantConfigQuery request, CancellationToken cancellationToken)
    {
        return _tenantConfigApplicationService.GetAsync(request.TenantId, cancellationToken);
    }
}
