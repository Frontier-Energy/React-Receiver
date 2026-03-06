using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("tenant-config")]
public sealed class TenantConfigController : ControllerBase
{
    private readonly ITenantConfigHandler _tenantConfigHandler;

    public TenantConfigController(ITenantConfigHandler tenantConfigHandler)
    {
        _tenantConfigHandler = tenantConfigHandler;
    }

    [HttpGet]
    public async Task<ActionResult<TenantBootstrapResponse>> GetTenantConfig([FromQuery] TenantConfigQueryRequest request)
    {
        var tenantConfig = await _tenantConfigHandler.GetTenantConfigAsync(request.TenantId, HttpContext.RequestAborted);
        return tenantConfig is null ? NotFound() : Ok(tenantConfig);
    }

    [HttpPost]
    public async Task<ActionResult<TenantBootstrapResponse>> UpsertTenantConfig([FromBody] TenantBootstrapResponse request)
    {
        var tenantConfig = await _tenantConfigHandler.UpsertTenantConfigAsync(request, HttpContext.RequestAborted);
        return Ok(tenantConfig);
    }
}
