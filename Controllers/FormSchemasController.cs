using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("form-schemas")]
public sealed class FormSchemasController : ControllerBase
{
    private readonly IFormSchemaService _formSchemaService;
    private readonly ILogger<FormSchemasController> _logger;

    public FormSchemasController(
        IFormSchemaService formSchemaService,
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
        return response is null ? NotFound() : Ok(response);
    }
}
