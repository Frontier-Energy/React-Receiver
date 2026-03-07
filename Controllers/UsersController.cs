using MediatR;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Users;
using React_Receiver.Models;

namespace React_Receiver.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("lookup")]
    public async Task<ActionResult<GetUserResponse>> GetUser([FromBody] GetUserRequest request)
    {
        var response = await _sender.Send(new GetUserQuery(request.UserId!), HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> GetCurrentUser()
    {
        var response = await _sender.Send(new GetCurrentUserQuery(), HttpContext.RequestAborted);
        return Ok(response);
    }
}
