using Microsoft.AspNetCore.Mvc;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserQueryService _userQueryService;

    public UsersController(IUserQueryService userQueryService)
    {
        _userQueryService = userQueryService;
    }

    [HttpPost("lookup")]
    [HttpPost("/QHVAC/GetUser")]
    public async Task<ActionResult<GetUserResponse>> GetUser([FromBody] GetUserRequest request)
    {
        var response = await _userQueryService.GetUserAsync(request.UserId!, HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("me")]
    [HttpGet("/QHVAC/me")]
    public async Task<ActionResult<MeResponse>> GetCurrentUser()
    {
        var response = await _userQueryService.GetCurrentUserAsync(HttpContext.RequestAborted);
        return Ok(response);
    }
}
