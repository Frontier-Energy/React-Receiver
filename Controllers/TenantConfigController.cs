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
    public async Task<ActionResult<TenantBootstrapResponse>> GetTenantConfig([FromQuery] string? tenantId)
    {
        var tenantConfig = await _tenantConfigHandler.GetTenantConfigAsync(tenantId, HttpContext.RequestAborted);
        return tenantConfig is null ? NotFound() : Ok(tenantConfig);
    }

    [HttpPost]
    public async Task<ActionResult<TenantBootstrapResponse>> UpsertTenantConfig([FromBody] TenantBootstrapResponse request)
    {
        if (!IsValid(request))
        {
            return BadRequest("A valid tenant bootstrap payload is required.");
        }

        var tenantConfig = await _tenantConfigHandler.UpsertTenantConfigAsync(request, HttpContext.RequestAborted);
        return Ok(tenantConfig);
    }

    private static bool IsValid(TenantBootstrapResponse? request)
    {
        return request is not null &&
               !string.IsNullOrWhiteSpace(request.TenantId) &&
               !string.IsNullOrWhiteSpace(request.DisplayName) &&
               request.UiDefaults is not null &&
               !string.IsNullOrWhiteSpace(request.UiDefaults.Theme) &&
               !string.IsNullOrWhiteSpace(request.UiDefaults.Font) &&
               !string.IsNullOrWhiteSpace(request.UiDefaults.Language) &&
               request.EnabledForms is not null;
    }
}
