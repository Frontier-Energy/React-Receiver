using MediatR;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("form-schemas")]
public sealed class FormSchemasController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<FormSchemasController> _logger;

    public FormSchemasController(ISender sender, ILogger<FormSchemasController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<FormSchemaCatalogResponse>> ListFormSchemas()
    {
        var response = await _sender.Send(new ListFormSchemasQuery(), HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> GetFormSchema([FromRoute] FormSchemaRouteRequest request)
    {
        var response = await _sender.Send(new GetFormSchemaQuery(request.FormType!), HttpContext.RequestAborted);
        if (response is null)
        {
            return NotFound();
        }

        WriteConcurrencyHeaders(response.ETag, response.Version);
        return Ok(response.Resource);
    }

    [HttpPut("{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> UpsertFormSchema(
        [FromRoute] FormSchemaRouteRequest routeRequest,
        [FromBody] FormSchemaResponse request)
    {
        _logger.LogInformation(
            "Processing form schema upsert for {FormType}",
            routeRequest.FormType);
        var response = await _sender.Send(
            new UpsertFormSchemaCommand(routeRequest.FormType!, request, Request.Headers.IfMatch.ToString()),
            HttpContext.RequestAborted);
        WriteConcurrencyHeaders(response.ETag, response.Version);

        if (response.Created)
        {
            _logger.LogInformation(
                "Created form schema for {FormType} with version {Version}",
                routeRequest.FormType,
                response.Version);
            return CreatedAtAction(
                nameof(GetFormSchema),
                new { formType = routeRequest.FormType },
                response.Resource);
        }

        Response.Headers.ContentLocation = $"/form-schemas/{Uri.EscapeDataString(routeRequest.FormType!)}";
        _logger.LogInformation(
            "Updated form schema for {FormType} with version {Version}",
            routeRequest.FormType,
            response.Version);

        return Ok(response.Resource);
    }

    private void WriteConcurrencyHeaders(string? etag, string? version)
    {
        if (!string.IsNullOrWhiteSpace(etag))
        {
            Response.Headers.ETag = etag;
        }

        if (!string.IsNullOrWhiteSpace(version))
        {
            Response.Headers["X-Form-Schema-Version"] = version;
        }
    }
}
