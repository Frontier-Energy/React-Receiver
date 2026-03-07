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
    private readonly ILogger<UsersController> _logger;

    public UsersController(ISender sender, ILogger<UsersController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpPost("lookup")]
    public async Task<ActionResult<GetUserResponse>> GetUser([FromBody] GetUserRequest request)
    {
        _logger.LogInformation("Processing user lookup for {UserId}", request.UserId);
        var response = await _sender.Send(new GetUserQuery(request.UserId!), HttpContext.RequestAborted);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> GetCurrentUser()
    {
        _logger.LogInformation("Processing current user lookup");
        var response = await _sender.Send(new GetCurrentUserQuery(), HttpContext.RequestAborted);
        return Ok(response);
    }
}
