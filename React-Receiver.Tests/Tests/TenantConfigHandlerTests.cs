using React_Receiver.Application.TenantConfig;
using React_Receiver.Domain.Tenants;
using React_Receiver.Models;
using React_Receiver.Services;
using React_Receiver.Infrastructure.TenantConfig;
using Xunit;

namespace React_Receiver.Tests;

public sealed class TenantConfigHandlerTests
{
    [Fact]
    public async Task GetAsync_ReturnsDefaultPayload_WhenRepositoryDisabled()
    {
        var handler = CreateHandler();

        var result = await handler.GetAsync(null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("qhvac", result.TenantId);
        Assert.Equal("QHVAC", result.DisplayName);
        Assert.Equal("harbor", result.UiDefaults.Theme);
        Assert.Equal(["electrical", "electrical-sf", "hvac"], result.EnabledForms);
        Assert.True(result.LoginRequired);
    }

    [Fact]
    public async Task UpsertAsync_ReturnsNormalizedPayload_WhenRepositoryDisabled()
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

        var result = await handler.UpsertAsync(payload, CancellationToken.None);

        Assert.Equal("custom-tenant", result.TenantId);
        Assert.Equal("Custom Tenant", result.DisplayName);
        Assert.Equal("custom-theme", result.UiDefaults.Theme);
        Assert.Equal(["hvac"], result.EnabledForms);
        Assert.False(result.LoginRequired);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForUnknownTenant_WhenRepositoryDisabled()
    {
        var handler = CreateHandler();

        var result = await handler.GetAsync("unknown", CancellationToken.None);

        Assert.Null(result);
    }

    private static TenantConfigApplicationService CreateHandler()
    {
        return new TenantConfigApplicationService(new DisabledTenantConfigRepository(), new FileBootstrapDataProvider());
    }

    private sealed class DisabledTenantConfigRepository : ITenantConfigRepository
    {
        public bool IsConfigured => false;

        public Task<TenantConfiguration?> GetAsync(string tenantId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ExistsAsync(string tenantId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TenantConfiguration> UpsertAsync(TenantConfiguration tenantConfiguration, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
