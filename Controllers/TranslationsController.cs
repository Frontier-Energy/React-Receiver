using Microsoft.AspNetCore.Mvc;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("translations")]
public sealed class TranslationsController : ControllerBase
{
    private readonly ITranslationService _translationService;

    public TranslationsController(ITranslationService translationService)
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
        return response is null ? NotFound() : Ok(response);
    }
}
