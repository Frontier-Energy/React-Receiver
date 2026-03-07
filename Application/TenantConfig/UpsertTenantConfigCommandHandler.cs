using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.TenantConfig;

public sealed class UpsertTenantConfigCommandHandler : IRequestHandler<UpsertTenantConfigCommand, UpsertResult<TenantBootstrapResponse>>
{
    private readonly ITenantConfigApplicationService _tenantConfigApplicationService;

    public UpsertTenantConfigCommandHandler(ITenantConfigApplicationService tenantConfigApplicationService)
    {
        _tenantConfigApplicationService = tenantConfigApplicationService;
    }

    public Task<UpsertResult<TenantBootstrapResponse>> Handle(UpsertTenantConfigCommand request, CancellationToken cancellationToken)
    {
        return _tenantConfigApplicationService.UpsertAsync(
            request.Request.TenantId ?? string.Empty,
            request.Request,
            request.ExpectedETag,
            cancellationToken);
    }
}
