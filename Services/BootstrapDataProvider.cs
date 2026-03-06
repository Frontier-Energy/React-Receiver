using System.Text.Json;
using React_Receiver.Models;

namespace React_Receiver.Services;

public interface IBootstrapDataProvider
{
    IReadOnlyCollection<FormSchemaSeed> GetFormSchemas();
    IReadOnlyCollection<TranslationSeed> GetTranslations();
    IReadOnlyCollection<TenantConfigSeed> GetTenantConfigs();
}

public sealed record FormSchemaSeed(
    string FormType,
    string Version,
    string Etag,
    FormSchemaResponse Schema);

public sealed record TranslationSeed(
    string Language,
    TranslationsResponse Payload);

public sealed record TenantConfigSeed(
    TenantBootstrapResponse Payload);

public sealed class FileBootstrapDataProvider : IBootstrapDataProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly BootstrapDataDocument _document;

    public FileBootstrapDataProvider()
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "SeedData", "bootstrap-data.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Bootstrap seed data was not found at '{filePath}'.", filePath);
        }

        using var stream = File.OpenRead(filePath);
        _document = JsonSerializer.Deserialize<BootstrapDataDocument>(stream, SerializerOptions)
            ?? new BootstrapDataDocument();
    }

    public IReadOnlyCollection<FormSchemaSeed> GetFormSchemas()
    {
        return _document.FormSchemas
            .Select(item => new FormSchemaSeed(
                item.FormType,
                item.Version,
                item.Etag,
                Clone(item.Schema)))
            .ToArray();
    }

    public IReadOnlyCollection<TranslationSeed> GetTranslations()
    {
        return _document.Translations
            .Select(item => new TranslationSeed(item.Language, Clone(item.Payload)))
            .ToArray();
    }

    public IReadOnlyCollection<TenantConfigSeed> GetTenantConfigs()
    {
        return _document.TenantConfigs
            .Select(item => new TenantConfigSeed(Clone(item)))
            .ToArray();
    }

    private static FormSchemaResponse Clone(FormSchemaResponse response)
    {
        return JsonSerializer.Deserialize<FormSchemaResponse>(
                   JsonSerializer.Serialize(response, SerializerOptions),
                   SerializerOptions)
               ?? new FormSchemaResponse(string.Empty, []);
    }

    private static TranslationsResponse Clone(TranslationsResponse response)
    {
        return JsonSerializer.Deserialize<TranslationsResponse>(
                   JsonSerializer.Serialize(response, SerializerOptions),
                   SerializerOptions)
               ?? new TranslationsResponse();
    }

    private static TenantBootstrapResponse Clone(TenantBootstrapResponse response)
    {
        return JsonSerializer.Deserialize<TenantBootstrapResponse>(
                   JsonSerializer.Serialize(response, SerializerOptions),
                   SerializerOptions)
               ?? new TenantBootstrapResponse(string.Empty, string.Empty, new UiDefaults(string.Empty, string.Empty, string.Empty, false, false, false), [], false);
    }

    private sealed class BootstrapDataDocument
    {
        public List<FormSchemaSeedDocument> FormSchemas { get; set; } = [];
        public List<TranslationSeedDocument> Translations { get; set; } = [];
        public List<TenantBootstrapResponse> TenantConfigs { get; set; } = [];
    }

    private sealed class FormSchemaSeedDocument
    {
        public string FormType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Etag { get; set; } = string.Empty;
        public FormSchemaResponse Schema { get; set; } = new(string.Empty, []);
    }

    private sealed class TranslationSeedDocument
    {
        public string Language { get; set; } = string.Empty;
        public TranslationsResponse Payload { get; set; } = new();
    }
}
