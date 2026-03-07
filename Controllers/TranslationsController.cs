using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Translations;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("translations")]
public sealed class TranslationsController : ControllerBase
{
    private readonly ITranslationApplicationService _translationService;

    public TranslationsController(ITranslationApplicationService translationService)
    {
        _translationService = translationService;
    }

    [HttpGet("{language}")]
    public async Task<ActionResult<TranslationsResponse>> GetTranslations([FromRoute] TranslationLanguageRequest request)
    {
        var response = await _translationService.GetAsync(request.Language!, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{language}")]
    public async Task<ActionResult<TranslationsResponse>> UpsertTranslations(
        [FromRoute] TranslationLanguageRequest routeRequest,
        [FromBody] TranslationsResponse request)
    {
        var response = await _translationService.UpsertAsync(routeRequest.Language!, request, HttpContext.RequestAborted);
        if (response.Created)
        {
            return CreatedAtAction(
                nameof(GetTranslations),
                new { language = routeRequest.Language },
                response.Resource);
        }

        Response.Headers.ContentLocation = $"/translations/{Uri.EscapeDataString(routeRequest.Language!)}";

        return Ok(response.Resource);
    }
}
