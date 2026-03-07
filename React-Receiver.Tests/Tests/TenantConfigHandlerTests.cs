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

        var result = await handler.UpsertAsync(" custom-tenant ", payload, CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal("custom-tenant", result.Resource.TenantId);
        Assert.Equal("Custom Tenant", result.Resource.DisplayName);
        Assert.Equal("custom-theme", result.Resource.UiDefaults.Theme);
        Assert.Equal(["hvac"], result.Resource.EnabledForms);
        Assert.False(result.Resource.LoginRequired);
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
        return new TenantConfigApplicationService(
            new DisabledTenantConfigRepository(),
            new TenantConfigSeedStore(new FileBootstrapDataProvider()));
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
