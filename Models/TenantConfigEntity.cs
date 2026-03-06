using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class TenantConfigEntity : ITableEntity
{
    public const string PartitionKeyValue = "TenantConfig";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string TenantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
    public string Font { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool ShowLeftFlyout { get; set; }
    public bool ShowRightFlyout { get; set; }
    public bool ShowInspectionStatsButton { get; set; }
    public string EnabledFormsJson { get; set; } = "[]";
    public bool LoginRequired { get; set; }

    public TenantBootstrapResponse ToResponse()
    {
        var enabledForms = JsonSerializer.Deserialize<string[]>(
            EnabledFormsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? Array.Empty<string>();

        return new TenantBootstrapResponse(
            TenantId: TenantId,
            DisplayName: DisplayName,
            UiDefaults: new UiDefaults(
                Theme: Theme,
                Font: Font,
                Language: Language,
                ShowLeftFlyout: ShowLeftFlyout,
                ShowRightFlyout: ShowRightFlyout,
                ShowInspectionStatsButton: ShowInspectionStatsButton),
            EnabledForms: enabledForms,
            LoginRequired: LoginRequired);
    }

    public static TenantConfigEntity FromResponse(TenantBootstrapResponse response)
    {
        return new TenantConfigEntity
        {
            PartitionKey = PartitionKeyValue,
            RowKey = response.TenantId,
            TenantId = response.TenantId,
            DisplayName = response.DisplayName,
            Theme = response.UiDefaults.Theme,
            Font = response.UiDefaults.Font,
            Language = response.UiDefaults.Language,
            ShowLeftFlyout = response.UiDefaults.ShowLeftFlyout,
            ShowRightFlyout = response.UiDefaults.ShowRightFlyout,
            ShowInspectionStatsButton = response.UiDefaults.ShowInspectionStatsButton,
            EnabledFormsJson = JsonSerializer.Serialize(response.EnabledForms),
            LoginRequired = response.LoginRequired
        };
    }
}
