using MediatR;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("tenant-config")]
public sealed class TenantConfigController : ControllerBase
{
    private readonly ISender _sender;

    public TenantConfigController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<ActionResult<TenantBootstrapResponse>> GetTenantConfig([FromQuery] TenantConfigQueryRequest request)
    {
        var tenantConfig = await _sender.Send(new GetTenantConfigQuery(request.TenantId), HttpContext.RequestAborted);
        return tenantConfig is null ? NotFound() : Ok(tenantConfig);
    }

    [HttpPost]
    public async Task<ActionResult<TenantBootstrapResponse>> UpsertTenantConfig([FromBody] TenantBootstrapResponse request)
    {
        var tenantConfig = await _sender.Send(new UpsertTenantConfigCommand(request), HttpContext.RequestAborted);
        return Ok(tenantConfig);
    }
}
