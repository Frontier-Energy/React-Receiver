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
    public async Task<ActionResult<TranslationsResponse>> GetTranslations([FromRoute] string language)
    {
        var response = await _sender.Send(new GetTranslationsQuery(language), HttpContext.RequestAborted);
        if (response is null)
        {
            return NotFound();
        }

        WriteConcurrencyHeaders(response.ETag);
        return Ok(response.Resource);
    }

    [HttpPut("{language}")]
    public async Task<ActionResult<TranslationsResponse>> UpsertTranslations(
        [FromRoute] string language,
        [FromBody] TranslationsResponse request)
    {
        _logger.LogInformation(
            "Processing translations upsert for {Language}",
            language);
        var response = await _sender.Send(
            new UpsertTranslationsCommand(language, request, Request.Headers.IfMatch.ToString()),
            HttpContext.RequestAborted);
        WriteConcurrencyHeaders(response.ETag);
        if (response.Created)
        {
            _logger.LogInformation(
                "Created translations for {Language}",
                language);
            return CreatedAtAction(
                nameof(GetTranslations),
                new { language },
                response.Resource);
        }

        Response.Headers.ContentLocation = $"/translations/{Uri.EscapeDataString(language)}";
        _logger.LogInformation(
            "Updated translations for {Language}",
            language);

        return Ok(response.Resource);
    }

    private void WriteConcurrencyHeaders(string? etag)
    {
        if (!string.IsNullOrWhiteSpace(etag))
        {
            Response.Headers.ETag = etag;
        }
    }
}
