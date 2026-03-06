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
    public async Task<ActionResult<TranslationsResponse>> GetTranslations([FromRoute] string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return BadRequest("language is required.");
        }

        var response = await _translationService.GetAsync(language, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{language}")]
    public async Task<ActionResult<TranslationsResponse>> UpsertTranslations(
        [FromRoute] string language,
        [FromBody] TranslationsResponse request)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return BadRequest("language is required.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.LanguageName) || request.App is null)
        {
            return BadRequest("A valid translations payload is required.");
        }

        var response = await _translationService.UpsertAsync(language, request, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }
}
