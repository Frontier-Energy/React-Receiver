using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class TenantConfigHandlerTests
{
    [Fact]
    public async Task GetTenantConfigAsync_ReturnsDefaultPayload_WhenConnectionStringEmpty()
    {
        var handler = CreateHandler();

        var result = await handler.GetTenantConfigAsync(null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("qhvac", result.TenantId);
        Assert.Equal("QHVAC", result.DisplayName);
        Assert.Equal("harbor", result.UiDefaults.Theme);
        Assert.Equal(["electrical", "electrical-sf", "hvac"], result.EnabledForms);
        Assert.True(result.LoginRequired);
    }

    [Fact]
    public async Task UpsertTenantConfigAsync_ReturnsNormalizedPayload_WhenConnectionStringEmpty()
    {
        var handler = CreateHandler();
        var payload = new TenantBootstrapResponse(
            TenantId: " custom-tenant ",
            DisplayName: " Custom Tenant ",
            UiDefaults: new UiDefaults(
                Theme: "custom-theme",
                Font: "Segoe UI",
                Language: "en",
                ShowLeftFlyout: false,
                ShowRightFlyout: true,
                ShowInspectionStatsButton: true),
            EnabledForms: ["hvac"],
            LoginRequired: false);

        var result = await handler.UpsertTenantConfigAsync(payload, CancellationToken.None);

        Assert.Equal("custom-tenant", result.TenantId);
        Assert.Equal("Custom Tenant", result.DisplayName);
        Assert.Equal("custom-theme", result.UiDefaults.Theme);
        Assert.Equal(["hvac"], result.EnabledForms);
        Assert.False(result.LoginRequired);
    }

    [Fact]
    public async Task GetTenantConfigAsync_ReturnsNull_ForUnknownTenant_WhenConnectionStringEmpty()
    {
        var handler = CreateHandler();

        var result = await handler.GetTenantConfigAsync("unknown", CancellationToken.None);

        Assert.Null(result);
    }

    private static TenantConfigHandler CreateHandler()
    {
        var tableClient = new TableServiceClient("UseDevelopmentStorage=true");
        var options = Options.Create(new TableStorageOptions { ConnectionString = string.Empty });
        return new TenantConfigHandler(tableClient, new FileBootstrapDataProvider(), options);
    }
}
