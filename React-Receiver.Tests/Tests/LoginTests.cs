using System;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using React_Receiver.Controllers;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;
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
        var tableClient = new TableServiceClient("UseDevelopmentStorage=true");
        var options = Options.Create(new TableStorageOptions { ConnectionString = string.Empty });
        var controller = new QHVACController(new FakeInspectionRequestHandler(), tableClient, options)
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
        public Task SaveRequestAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
