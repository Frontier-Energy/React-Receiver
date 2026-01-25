using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Controllers;
using React_Receiver.Handlers;
using React_Receiver.Models;
using Xunit;

namespace React_Receiver.Tests;

public sealed class LoginTests
{
    [Fact]
    public void Login_ReturnsOkWithGeneratedUserId()
    {
        var controller = CreateController();

        var result = controller.Login(new LoginRequestCommand("user@example.com"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginRequestResponse>(ok.Value);
        Assert.True(Guid.TryParseExact(response.UserId, "N", out _));
    }

    private static QHVACController CreateController()
    {
        var controller = new QHVACController(
            new FakeInspectionRequestHandler(),
            new FakeLoginRequestHandler(),
            new ReceiveInspectionRequestParser(),
            new FakeRegisterRequestHandler())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private sealed class FakeInspectionRequestHandler : IInspectionRequestHandler
    {
        public Task<ReceiveInspectionResponse> SaveRequestAsync(
            ReceiveInspectionRequest request,
            CancellationToken cancellationToken)
        {
            var response = new ReceiveInspectionResponse(
                Status: "Received",
                SessionId: request.SessionId ?? string.Empty,
                Name: request.Name ?? string.Empty,
                QueryParams: request.QueryParams ?? new Dictionary<string, string>(),
                Message: "OK");
            return Task.FromResult(response);
        }
    }

    private sealed class FakeLoginRequestHandler : ILoginRequestHandler
    {
        public LoginRequestResponse HandleLogin(LoginRequestCommand request)
        {
            return new LoginRequestResponse(UserId: Guid.NewGuid().ToString("N"));
        }
    }

    private sealed class FakeRegisterRequestHandler : IRegisterRequestHandler
    {
        public Task<string> HandleRegisterAsync(
            RegisterRequestModel request,
            string userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(userId);
        }
    }
}
