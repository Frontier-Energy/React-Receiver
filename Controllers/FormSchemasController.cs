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
        return response is null ? NotFound() : Ok(response);
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
            new UpsertFormSchemaCommand(routeRequest.FormType!, request),
            HttpContext.RequestAborted);
        if (!string.IsNullOrWhiteSpace(response.ETag))
        {
            Response.Headers.ETag = response.ETag;
        }

        if (!string.IsNullOrWhiteSpace(response.Version))
        {
            Response.Headers["X-Form-Schema-Version"] = response.Version;
        }

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
}
