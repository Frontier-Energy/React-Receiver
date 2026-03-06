using Microsoft.AspNetCore.Mvc;
using React_Receiver.Handlers;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ILoginRequestHandler _loginRequestHandler;
    private readonly IRegisterRequestHandler _registerRequestHandler;

    public AuthController(
        ILoginRequestHandler loginRequestHandler,
        IRegisterRequestHandler registerRequestHandler)
    {
        _loginRequestHandler = loginRequestHandler;
        _registerRequestHandler = registerRequestHandler;
    }

    [HttpPost("login")]
    [HttpPost("/QHVAC/Login")]
    public ActionResult<LoginRequestResponse> Login([FromBody] LoginRequestCommand request)
    {
        var response = _loginRequestHandler.HandleLogin(request);
        return Ok(response);
    }

    [HttpPost("register")]
    [HttpPost("/QHVAC/Register")]
    public async Task<ActionResult<RegisterResponseModel>> Register([FromBody] RegisterRequestModel request)
    {
        var userId = Guid.NewGuid().ToString("N");
        userId = await _registerRequestHandler.HandleRegisterAsync(
            request,
            userId,
            HttpContext.RequestAborted);

        return Ok(new RegisterResponseModel(UserId: userId));
    }
}
