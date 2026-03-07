using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Controllers;
using React_Receiver.Models;
using React_Receiver.Tests.TestDoubles;
using Xunit;

namespace React_Receiver.Tests;

public sealed class TenantConfigTests
{
    [Fact]
    public async Task GetTenantConfig_ReturnsExpectedPayload()
    {
        var controller = CreateController();

        var result = await controller.GetTenantConfig(new TenantConfigQueryRequest());

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

    [Fact]
    public async Task GetTenantConfig_ReturnsNotFoundForUnknownTenant()
    {
        var controller = CreateController();

        var result = await controller.GetTenantConfig(new TenantConfigQueryRequest { TenantId = "unknown" });

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private static TenantConfigController CreateController()
    {
        var controller = new TenantConfigController(new TestSender((request, _) =>
        {
            return request switch
            {
                GetTenantConfigQuery query when !string.IsNullOrWhiteSpace(query.TenantId) &&
                                               !string.Equals(query.TenantId, "qhvac", StringComparison.OrdinalIgnoreCase)
                    => Task.FromResult<object?>(null),
                GetTenantConfigQuery => Task.FromResult<object?>(new TenantBootstrapResponse(
                    TenantId: "qhvac",
                    DisplayName: "QHVAC",
                    UiDefaults: new UiDefaults(
                        Theme: "harbor",
                        Font: "Tahoma, \"Trebuchet MS\", Arial, sans-serif",
                        Language: "en",
                        ShowLeftFlyout: true,
                        ShowRightFlyout: true,
                        ShowInspectionStatsButton: false),
                    EnabledForms: ["electrical", "electrical-sf", "hvac"],
                    LoginRequired: true)),
                UpsertTenantConfigCommand command => Task.FromResult<object?>(command.Request),
                _ => throw new NotSupportedException()
            };
        }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }
}
