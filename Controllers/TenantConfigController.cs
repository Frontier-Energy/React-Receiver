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

    [HttpPut("{tenantId}")]
    public async Task<ActionResult<TenantBootstrapResponse>> UpsertTenantConfig(
        [FromRoute] TenantConfigRouteRequest routeRequest,
        [FromBody] TenantBootstrapResponse request)
    {
        var response = await _sender.Send(
            new UpsertTenantConfigCommand(request with { TenantId = routeRequest.TenantId }),
            HttpContext.RequestAborted);
        if (response.Created)
        {
            return CreatedAtAction(
                nameof(GetTenantConfig),
                new { tenantId = routeRequest.TenantId },
                response.Resource);
        }

        Response.Headers.ContentLocation = $"/tenant-config/{Uri.EscapeDataString(routeRequest.TenantId!)}";

        return Ok(response.Resource);
    }
}
