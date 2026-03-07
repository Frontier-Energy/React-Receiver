using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task UpsertTenantConfig_ReturnsOkForKnownTenant()
    {
        var controller = CreateController();

        var payload = new TenantBootstrapResponse(
            TenantId: "ignored-by-route",
            DisplayName: "QHVAC Updated",
            UiDefaults: new UiDefaults(
                Theme: "harbor",
                Font: "Tahoma, \"Trebuchet MS\", Arial, sans-serif",
                Language: "en",
                ShowLeftFlyout: true,
                ShowRightFlyout: true,
                ShowInspectionStatsButton: false),
            EnabledForms: ["electrical", "hvac"],
            LoginRequired: true);

        var result = await controller.UpsertTenantConfig(new TenantConfigRouteRequest { TenantId = "qhvac" }, payload);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TenantBootstrapResponse>(ok.Value);
        Assert.Equal("qhvac", response.TenantId);
        Assert.Equal("/tenant-config/qhvac", controller.Response.Headers.ContentLocation.ToString());
    }

    [Fact]
    public async Task UpsertTenantConfig_ReturnsCreatedForUnknownTenant()
    {
        var controller = CreateController();

        var payload = new TenantBootstrapResponse(
            TenantId: null,
            DisplayName: "Custom Tenant",
            UiDefaults: new UiDefaults(
                Theme: "custom-theme",
                Font: "Segoe UI",
                Language: "en",
                ShowLeftFlyout: false,
                ShowRightFlyout: true,
                ShowInspectionStatsButton: true),
            EnabledForms: ["hvac"],
            LoginRequired: false);

        var result = await controller.UpsertTenantConfig(new TenantConfigRouteRequest { TenantId = "custom-tenant" }, payload);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(TenantConfigController.GetTenantConfig), created.ActionName);
        Assert.Equal("custom-tenant", created.RouteValues?["tenantId"]);
        var response = Assert.IsType<TenantBootstrapResponse>(created.Value);
        Assert.Equal("custom-tenant", response.TenantId);
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
                UpsertTenantConfigCommand command when string.Equals(command.Request.TenantId, "custom-tenant", StringComparison.OrdinalIgnoreCase)
                    => Task.FromResult<object?>(new UpsertResult<TenantBootstrapResponse>(
                        command.Request,
                        true)),
                UpsertTenantConfigCommand command
                    => Task.FromResult<object?>(new UpsertResult<TenantBootstrapResponse>(
                        command.Request,
                        false)),
                _ => throw new NotSupportedException()
            };
        }), NullLogger<TenantConfigController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }
}
