using System.Collections.Concurrent;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.FormSchemas;

public interface IFormSchemaSeedStore
{
    IReadOnlyCollection<FormSchemaCatalogItemResponse> ListCatalogItems();
    FormSchemaResponse? Get(string formType);
    UpsertResult<FormSchemaResponse> Upsert(string formType, FormSchemaResponse request);
    IReadOnlyCollection<KeyValuePair<string, FormSchemaResponse>> GetAll();
}

public sealed class FormSchemaSeedStore : IFormSchemaSeedStore
{
    private sealed record SeedEntry(string Version, string Etag, FormSchemaResponse Schema)
    {
        public FormSchemaCatalogItemResponse ToCatalogItem(string formType) => new(formType, Version, Etag);
    }

    private readonly ConcurrentDictionary<string, SeedEntry> _catalog;

    public FormSchemaSeedStore(IBootstrapDataProvider bootstrapDataProvider)
    {
        _catalog = new ConcurrentDictionary<string, SeedEntry>(
            bootstrapDataProvider
                .GetFormSchemas()
                .Select(item => new KeyValuePair<string, SeedEntry>(
                    item.FormType,
                    new SeedEntry(item.Version, item.Etag, item.Schema))),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<FormSchemaCatalogItemResponse> ListCatalogItems()
    {
        return _catalog.Select(item => item.Value.ToCatalogItem(item.Key)).ToArray();
    }

    public FormSchemaResponse? Get(string formType)
    {
        return _catalog.TryGetValue(formType, out var entry) ? entry.Schema : null;
    }

    public UpsertResult<FormSchemaResponse> Upsert(string formType, FormSchemaResponse request)
    {
        var now = DateTime.UtcNow;
        var created = !_catalog.ContainsKey(formType);
        _catalog[formType] = new SeedEntry(
            now.ToString("yyyy-MM-dd"),
            $"\"{formType}-{now:yyyyMMddHHmmssfff}\"",
            request);

        var entry = _catalog[formType];
        return new UpsertResult<FormSchemaResponse>(request, created, entry.Version, entry.Etag);
    }

    public IReadOnlyCollection<KeyValuePair<string, FormSchemaResponse>> GetAll()
    {
        return _catalog
            .Select(item => new KeyValuePair<string, FormSchemaResponse>(item.Key, item.Value.Schema))
            .ToArray();
    }
}
