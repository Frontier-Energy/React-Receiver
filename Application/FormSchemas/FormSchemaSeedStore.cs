using System.Collections.Concurrent;
using React_Receiver.Application.Concurrency;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.FormSchemas;

public interface IFormSchemaSeedStore
{
    IReadOnlyCollection<FormSchemaCatalogItemResponse> ListCatalogItems();
    ResourceEnvelope<FormSchemaResponse>? Get(string formType);
    UpsertResult<FormSchemaResponse> Upsert(string formType, FormSchemaResponse request, string? expectedETag);
    IReadOnlyCollection<KeyValuePair<string, FormSchemaResponse>> GetAll();
}

public sealed class FormSchemaSeedStore : IFormSchemaSeedStore
{
    private sealed record SeedEntry(string Version, string Etag, FormSchemaResponse Schema)
    {
        public FormSchemaCatalogItemResponse ToCatalogItem(string formType) => new(formType, Version, Etag);
    }

    private readonly ConcurrentDictionary<string, SeedEntry> _catalog;
    private readonly object _gate = new();

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

    public ResourceEnvelope<FormSchemaResponse>? Get(string formType)
    {
        return _catalog.TryGetValue(formType, out var entry)
            ? new ResourceEnvelope<FormSchemaResponse>(entry.Schema, entry.Etag, entry.Version)
            : null;
    }

    public UpsertResult<FormSchemaResponse> Upsert(string formType, FormSchemaResponse request, string? expectedETag)
    {
        lock (_gate)
        {
            _catalog.TryGetValue(formType, out var existing);
            OptimisticConcurrency.EnsureSatisfied(expectedETag, existing?.Etag, $"form schema '{formType}'");

            var now = DateTime.UtcNow;
            var created = existing is null;
            var entry = new SeedEntry(
                now.ToString("yyyy-MM-dd"),
                $"\"{formType}-{now:yyyyMMddHHmmssfff}\"",
                request);

            _catalog[formType] = entry;
            return new UpsertResult<FormSchemaResponse>(request, created, entry.Version, entry.Etag);
        }
    }

    public IReadOnlyCollection<KeyValuePair<string, FormSchemaResponse>> GetAll()
    {
        return _catalog
            .Select(item => new KeyValuePair<string, FormSchemaResponse>(item.Key, item.Value.Schema))
            .ToArray();
    }
}
