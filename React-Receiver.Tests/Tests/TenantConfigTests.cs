using System;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using React_Receiver.Controllers;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class TenantConfigTests
{
    [Fact]
    public void GetTenantConfig_ReturnsExpectedPayload()
    {
        var controller = CreateController();

        var result = controller.GetTenantConfig();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TenantBootstrapResponse>(ok.Value);
        Assert.Equal("qhvac", response.TenantId);
        Assert.Equal("QHVAC", response.DisplayName);
        Assert.Equal("harbor", response.UiDefaults.Theme);
        Assert.Equal("Tahoma, \"Trebuchet MS\", Arial, sans-serif", response.UiDefaults.Font);
        Assert.Equal("en", response.UiDefaults.Language);
        Assert.True(response.UiDefaults.ShowLeftFlyout);
        Assert.True(response.UiDefaults.ShowRightFlyout);
        Assert.False(response.UiDefaults.ShowInspectionStatsButton);
        Assert.Equal(["electrical", "electrical-sf", "hvac"], response.EnabledForms);
        Assert.True(response.LoginRequired);
    }

    private static QHVACController CreateController()
    {
        var controller = new QHVACController(
            new FakeInspectionRequestHandler(),
            new FakeLoginRequestHandler(),
            new ReceiveInspectionRequestParser(),
            new FakeRegisterRequestHandler(),
            new BlobServiceClient("UseDevelopmentStorage=true"),
            new TableServiceClient("UseDevelopmentStorage=true"),
            Options.Create(new BlobStorageOptions()),
            Options.Create(new TableStorageOptions()))
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
