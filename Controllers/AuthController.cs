using MediatR;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Auth;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ISender sender, ILogger<AuthController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginRequestResponse>> Login([FromBody] LoginRequestCommand request)
    {
        _logger.LogInformation(
            "Processing login request for {Email}",
            MaskEmail(request.Email));
        var response = await _sender.Send(new LoginCommand(request), HttpContext.RequestAborted);
        _logger.LogInformation(
            "Completed login request with {Outcome}",
            string.IsNullOrWhiteSpace(response.UserId) ? "rejected" : "success");
        return Ok(response);
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponseModel>> Register([FromBody] RegisterRequestModel request)
    {
        _logger.LogInformation(
            "Processing register request for {Email}",
            MaskEmail(request.Email));
        var response = await _sender.Send(new RegisterCommand(request), HttpContext.RequestAborted);
        _logger.LogInformation(
            "Completed register request for {UserId}",
            response.UserId);
        return Ok(response);
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var parts = email.Split('@', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return "***";
        }

        return $"{parts[0][0]}***@{parts[1]}";
    }
}
