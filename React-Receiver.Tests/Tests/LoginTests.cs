using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Auth;
using React_Receiver.Controllers;
using React_Receiver.Models;
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
        var controller = new AuthController(new FakeAuthApplicationService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private sealed class FakeAuthApplicationService : IAuthApplicationService
    {
        public Task<LoginRequestResponse> LoginAsync(LoginRequestCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LoginRequestResponse(UserId: Guid.NewGuid().ToString("N")));
        }

        public Task<RegisterResponseModel> RegisterAsync(
            RegisterRequestModel request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new RegisterResponseModel(Guid.NewGuid().ToString("N")));
        }
    }
}
