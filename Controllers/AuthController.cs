using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Auth;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthApplicationService _authApplicationService;

    public AuthController(IAuthApplicationService authApplicationService)
    {
        _authApplicationService = authApplicationService;
    }

    [HttpPost("login")]
    [HttpPost("/QHVAC/Login")]
    public async Task<ActionResult<LoginRequestResponse>> Login([FromBody] LoginRequestCommand request)
    {
        var response = await _authApplicationService.LoginAsync(request, HttpContext.RequestAborted);
        return Ok(response);
    }

    [HttpPost("register")]
    [HttpPost("/QHVAC/Register")]
    public async Task<ActionResult<RegisterResponseModel>> Register([FromBody] RegisterRequestModel request)
    {
        var response = await _authApplicationService.RegisterAsync(request, HttpContext.RequestAborted);
        return Ok(response);
    }
}
