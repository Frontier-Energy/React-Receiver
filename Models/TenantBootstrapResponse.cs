using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record TenantBootstrapResponse(
    [property: Required(AllowEmptyStrings = false)] string? TenantId,
    [property: Required(AllowEmptyStrings = false)] string? DisplayName,
    [property: Required] UiDefaults? UiDefaults,
    [property: Required] string[]? EnabledForms,
    bool LoginRequired
);

public sealed record UiDefaults(
    [property: Required(AllowEmptyStrings = false)] string? Theme,
    [property: Required(AllowEmptyStrings = false)] string? Font,
    [property: Required(AllowEmptyStrings = false)] string? Language,
    bool ShowLeftFlyout,
    bool ShowRightFlyout,
    bool ShowInspectionStatsButton
);
