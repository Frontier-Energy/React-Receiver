using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using React_Receiver.Application.Auth;
using React_Receiver.Controllers;
using React_Receiver.Models;
using React_Receiver.Tests.TestDoubles;
using Xunit;

namespace React_Receiver.Tests;

public sealed class LoginTests
{
    [Fact]
    public async Task Login_ReturnsOkWithGeneratedUserId()
    {
        var controller = CreateController();

        var result = await controller.Login(new LoginRequestCommand("user@example.com"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginRequestResponse>(ok.Value);
        Assert.True(Guid.TryParseExact(response.UserId, "N", out _));
    }

    private static AuthController CreateController()
    {
        var controller = new AuthController(new TestSender((request, _) =>
        {
            return request switch
            {
                LoginCommand => Task.FromResult<object?>(new LoginRequestResponse(Guid.NewGuid().ToString("N"))),
                RegisterCommand => Task.FromResult<object?>(new RegisterResponseModel(Guid.NewGuid().ToString("N"))),
                _ => throw new NotSupportedException()
            };
        }), NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }
}
