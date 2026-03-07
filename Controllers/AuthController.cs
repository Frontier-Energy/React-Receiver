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

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("login")]
    [HttpPost("/QHVAC/Login")]
    public async Task<ActionResult<LoginRequestResponse>> Login([FromBody] LoginRequestCommand request)
    {
        var response = await _sender.Send(new LoginCommand(request), HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpPost("register")]
    [HttpPost("/QHVAC/Register")]
    public async Task<ActionResult<RegisterResponseModel>> Register([FromBody] RegisterRequestModel request)
    {
        var response = await _sender.Send(new RegisterCommand(request), HttpContext.RequestAborted);
        return Ok(response);
    }
}
