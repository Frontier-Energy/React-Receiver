using MediatR;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Translations;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("translations")]
public sealed class TranslationsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<TranslationsController> _logger;

    public TranslationsController(ISender sender, ILogger<TranslationsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet("{language}")]
    public async Task<ActionResult<TranslationsResponse>> GetTranslations([FromRoute] TranslationLanguageRequest request)
    {
        var response = await _sender.Send(new GetTranslationsQuery(request.Language!), HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{language}")]
    public async Task<ActionResult<TranslationsResponse>> UpsertTranslations(
        [FromRoute] TranslationLanguageRequest routeRequest,
        [FromBody] TranslationsResponse request)
    {
        _logger.LogInformation(
            "Processing translations upsert for {Language}",
            routeRequest.Language);
        var response = await _sender.Send(
            new UpsertTranslationsCommand(routeRequest.Language!, request),
            HttpContext.RequestAborted);
        if (response.Created)
        {
            _logger.LogInformation(
                "Created translations for {Language}",
                routeRequest.Language);
            return CreatedAtAction(
                nameof(GetTranslations),
                new { language = routeRequest.Language },
                response.Resource);
        }

        Response.Headers.ContentLocation = $"/translations/{Uri.EscapeDataString(routeRequest.Language!)}";
        _logger.LogInformation(
            "Updated translations for {Language}",
            routeRequest.Language);

        return Ok(response.Resource);
    }
}
