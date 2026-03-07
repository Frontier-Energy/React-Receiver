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
    private readonly ILogger<TenantConfigController> _logger;

    public TenantConfigController(ISender sender, ILogger<TenantConfigController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TenantBootstrapResponse>> GetTenantConfig([FromQuery] TenantConfigQueryRequest request)
    {
        var tenantConfig = await _sender.Send(new GetTenantConfigQuery(request.TenantId), HttpContext.RequestAborted);
        if (tenantConfig is null)
        {
            return NotFound();
        }

        WriteConcurrencyHeaders(tenantConfig.ETag);
        return Ok(tenantConfig.Resource);
    }

    [HttpPut("{tenantId}")]
    public async Task<ActionResult<TenantBootstrapResponse>> UpsertTenantConfig(
        [FromRoute] TenantConfigRouteRequest routeRequest,
        [FromBody] TenantBootstrapResponse request)
    {
        _logger.LogInformation(
            "Processing tenant config upsert for {TenantId}",
            routeRequest.TenantId);
        var response = await _sender.Send(
            new UpsertTenantConfigCommand(
                request with { TenantId = routeRequest.TenantId },
                Request.Headers.IfMatch.ToString()),
            HttpContext.RequestAborted);
        WriteConcurrencyHeaders(response.ETag);
        if (response.Created)
        {
            _logger.LogInformation(
                "Created tenant config for {TenantId}",
                routeRequest.TenantId);
            return CreatedAtAction(
                nameof(GetTenantConfig),
                new { tenantId = routeRequest.TenantId },
                response.Resource);
        }

        Response.Headers.ContentLocation = $"/tenant-config/{Uri.EscapeDataString(routeRequest.TenantId!)}";
        _logger.LogInformation(
            "Updated tenant config for {TenantId}",
            routeRequest.TenantId);

        return Ok(response.Resource);
    }

    private void WriteConcurrencyHeaders(string? etag)
    {
        if (!string.IsNullOrWhiteSpace(etag))
        {
            Response.Headers.ETag = etag;
        }
    }
}
