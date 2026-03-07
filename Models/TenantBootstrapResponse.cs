using System.ComponentModel.DataAnnotations;

namespace React_Receiver.Models;

public sealed record TenantBootstrapResponse
{
    public TenantBootstrapResponse()
    {
    }

    public TenantBootstrapResponse(
        string? TenantId,
        string? DisplayName,
        UiDefaults? UiDefaults,
        string[]? EnabledForms,
        bool LoginRequired)
    {
        this.TenantId = TenantId;
        this.DisplayName = DisplayName;
        this.UiDefaults = UiDefaults;
        this.EnabledForms = EnabledForms;
        this.LoginRequired = LoginRequired;
    }

    [Required(AllowEmptyStrings = false)]
    public string? TenantId { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? DisplayName { get; init; }

    [Required]
    public UiDefaults? UiDefaults { get; init; }

    [Required]
    public string[]? EnabledForms { get; init; }

    public bool LoginRequired { get; init; }
}

public sealed record UiDefaults
{
    public UiDefaults()
    {
    }

    public UiDefaults(
        string? Theme,
        string? Font,
        string? Language,
        bool ShowLeftFlyout,
        bool ShowRightFlyout,
        bool ShowInspectionStatsButton)
    {
        this.Theme = Theme;
        this.Font = Font;
        this.Language = Language;
        this.ShowLeftFlyout = ShowLeftFlyout;
        this.ShowRightFlyout = ShowRightFlyout;
        this.ShowInspectionStatsButton = ShowInspectionStatsButton;
    }

    [Required(AllowEmptyStrings = false)]
    public string? Theme { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? Font { get; init; }

    [Required(AllowEmptyStrings = false)]
    public string? Language { get; init; }

    public bool ShowLeftFlyout { get; init; }

    public bool ShowRightFlyout { get; init; }

    public bool ShowInspectionStatsButton { get; init; }
}
