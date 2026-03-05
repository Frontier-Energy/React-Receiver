namespace React_Receiver.Models;

public sealed record TenantBootstrapResponse(
    string TenantId,
    string DisplayName,
    UiDefaults UiDefaults,
    string[] EnabledForms,
    bool LoginRequired
);

public sealed record UiDefaults(
    string Theme,
    string Font,
    string Language,
    bool ShowLeftFlyout,
    bool ShowRightFlyout,
    bool ShowInspectionStatsButton
);
