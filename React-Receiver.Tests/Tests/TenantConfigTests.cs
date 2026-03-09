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
        var uiDefaults = Assert.IsType<UiDefaults>(response.UiDefaults);
        var enabledForms = Assert.IsType<string[]>(response.EnabledForms);
        Assert.Equal("qhvac", response.TenantId);
        Assert.Equal("QHVAC", response.DisplayName);
        Assert.Equal("harbor", uiDefaults.Theme);
        Assert.Equal("Tahoma, \"Trebuchet MS\", Arial, sans-serif", uiDefaults.Font);
        Assert.Equal("en", uiDefaults.Language);
        Assert.True(uiDefaults.ShowLeftFlyout);
        Assert.True(uiDefaults.ShowRightFlyout);
        Assert.False(uiDefaults.ShowInspectionStatsButton);
        Assert.Equal(["electrical", "electrical-sf", "hvac"], enabledForms);
        Assert.True(response.LoginRequired);
        Assert.Equal("\"qhvac-v1\"", controller.Response.Headers.ETag.ToString());
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
        Assert.Equal("\"qhvac-v2\"", controller.Response.Headers.ETag.ToString());
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
        Assert.Equal("\"custom-tenant-v1\"", controller.Response.Headers.ETag.ToString());
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
                GetTenantConfigQuery => Task.FromResult<object?>(new ResourceEnvelope<TenantBootstrapResponse>(
                    new TenantBootstrapResponse(
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
                        LoginRequired: true),
                    "\"qhvac-v1\"")),
                UpsertTenantConfigCommand command when string.Equals(command.Request.TenantId, "custom-tenant", StringComparison.OrdinalIgnoreCase)
                    => Task.FromResult<object?>(new UpsertResult<TenantBootstrapResponse>(
                        command.Request,
                        true,
                        ETag: "\"custom-tenant-v1\"")),
                UpsertTenantConfigCommand command
                    => Task.FromResult<object?>(new UpsertResult<TenantBootstrapResponse>(
                        command.Request,
                        false,
                        ETag: "\"qhvac-v2\"")),
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
