using System.Text.Json;
using System.Text.Json.Serialization;

namespace React_Receiver.Models;

public sealed class TranslationsResponse
{
    public string? LanguageName { get; set; }
    public CommonTranslations? Common { get; set; }
    public AppTranslations? App { get; set; }
    public NavTranslations? Nav { get; set; }
    public DrawersTranslations? Drawers { get; set; }
    public ConnectivityTranslations? Connectivity { get; set; }
    public InspectionStatsTranslations? InspectionStats { get; set; }
    public CustomizationTranslations? Customization { get; set; }
    public UploadStatusTranslations? UploadStatus { get; set; }
    public FormTypesTranslations? FormTypes { get; set; }
    public HomeTranslations? Home { get; set; }
    public LoginTranslations? Login { get; set; }
    public RegisterTranslations? Register { get; set; }
    public NewInspectionTranslations? NewInspection { get; set; }
    public NewFormTranslations? NewForm { get; set; }
    public MyInspectionsTranslations? MyInspections { get; set; }
    public FillFormTranslations? FillForm { get; set; }
    public DebugInspectionTranslations? DebugInspection { get; set; }
    public FormRendererTranslations? FormRenderer { get; set; }
}

public sealed class AppTranslations : TranslationSection
{
    public string? Title { get; set; }
    public string? PoweredBy { get; set; }
    public string? Brand { get; set; }
}

public sealed class CommonTranslations : TranslationSection { }
public sealed class NavTranslations : TranslationSection { }
public sealed class DrawersTranslations : TranslationSection { }
public sealed class ConnectivityTranslations : TranslationSection { }
public sealed class InspectionStatsTranslations : TranslationSection { }
public sealed class CustomizationTranslations : TranslationSection { }
public sealed class UploadStatusTranslations : TranslationSection { }
public sealed class FormTypesTranslations : TranslationSection { }
public sealed class HomeTranslations : TranslationSection { }
public sealed class LoginTranslations : TranslationSection { }
public sealed class RegisterTranslations : TranslationSection { }
public sealed class NewInspectionTranslations : TranslationSection { }
public sealed class NewFormTranslations : TranslationSection { }
public sealed class MyInspectionsTranslations : TranslationSection { }
public sealed class FillFormTranslations : TranslationSection { }
public sealed class DebugInspectionTranslations : TranslationSection { }
public sealed class FormRendererTranslations : TranslationSection { }

public abstract class TranslationSection
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
