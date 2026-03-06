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
    public async Task<ActionResult<FormSchemaResponse>> GetFormSchema([FromRoute] string formType)
    {
        if (string.IsNullOrWhiteSpace(formType))
        {
            return BadRequest("formType is required.");
        }

        var response = await _formSchemaService.GetAsync(formType, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{formType}")]
    public async Task<ActionResult<FormSchemaResponse>> UpsertFormSchema(
        [FromRoute] string formType,
        [FromBody] FormSchemaResponse request)
    {
        if (string.IsNullOrWhiteSpace(formType))
        {
            return BadRequest("formType is required.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.FormName) || request.Sections is null)
        {
            return BadRequest("A valid form schema payload is required.");
        }

        var response = await _formSchemaService.UpsertAsync(formType, request, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }
}
