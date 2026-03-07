using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("form-schemas")]
public sealed class FormSchemasController : ControllerBase
{
    private readonly IFormSchemaApplicationService _formSchemaService;
    private readonly ILogger<FormSchemasController> _logger;

    public FormSchemasController(
        IFormSchemaApplicationService formSchemaService,
        ILogger<FormSchemasController> logger)
    {
        _formSchemaService = formSchemaService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<FormSchemaCatalogResponse>> ListFormSchemas()
    {
        var response = await _formSchemaService.ListAsync(HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpGet("{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> GetFormSchema([FromRoute] FormSchemaRouteRequest request)
    {
        try
        {
            var response = await _formSchemaService.GetAsync(request.FormType!, HttpContext.RequestAborted);
            return response is null ? NotFound() : Ok(response);
        }
        catch (FormSchemaBlobContentException ex)
        {
            _logger.LogError(ex, "Failed to read schema content for form type '{FormType}'.", request.FormType);
            return Problem(
                title: "Schema content unavailable",
                detail: "Schema metadata exists, but the stored schema content could not be read.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> UpsertFormSchema(
        [FromRoute] FormSchemaRouteRequest routeRequest,
        [FromBody] FormSchemaResponse request)
    {
        var response = await _formSchemaService.UpsertAsync(routeRequest.FormType!, request, HttpContext.RequestAborted);
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
            return CreatedAtAction(
                nameof(GetFormSchema),
                new { formType = routeRequest.FormType },
                response.Resource);
        }

        Response.Headers.ContentLocation = $"/form-schemas/{Uri.EscapeDataString(routeRequest.FormType!)}";

        return Ok(response.Resource);
    }
}
