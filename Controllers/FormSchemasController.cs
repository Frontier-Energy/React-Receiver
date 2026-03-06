using Microsoft.AspNetCore.Mvc;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("form-schemas")]
public sealed class FormSchemasController : ControllerBase
{
    private readonly IFormSchemaService _formSchemaService;

    public FormSchemasController(IFormSchemaService formSchemaService)
    {
        _formSchemaService = formSchemaService;
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
        var response = await _formSchemaService.GetAsync(request.FormType!, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
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
