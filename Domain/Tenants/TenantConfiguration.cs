namespace React_Receiver.Domain.Tenants;

public sealed record TenantUiDefaults(
    string Theme,
    string Font,
    string Language,
    bool ShowLeftFlyout,
    bool ShowRightFlyout,
    bool ShowInspectionStatsButton
);

public sealed record TenantConfiguration(
    string TenantId,
    string DisplayName,
    TenantUiDefaults UiDefaults,
    string[] EnabledForms,
    bool LoginRequired
)
{
    public static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? "qhvac" : tenantId.Trim();
    }

    public static TenantConfiguration Normalize(TenantConfiguration configuration)
    {
        return new TenantConfiguration(
            configuration.TenantId.Trim(),
            configuration.DisplayName.Trim(),
            configuration.UiDefaults,
            configuration.EnabledForms,
            configuration.LoginRequired);
    }
}
