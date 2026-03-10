using System.Collections.Concurrent;
using React_Receiver.Application.Concurrency;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.Translations;

public interface ITranslationSeedStore
{
    ResourceEnvelope<TranslationsResponse>? Get(string language);
    UpsertResult<TranslationsResponse> Upsert(string language, TranslationsResponse request, string? expectedETag);
    void Store(string language, TranslationsResponse request, string? etag);
    IReadOnlyCollection<KeyValuePair<string, TranslationsResponse>> GetAll();
}

public sealed class TranslationSeedStore : ITranslationSeedStore
{
    private sealed record SeedEntry(string ETag, TranslationsResponse Resource);

    private readonly ConcurrentDictionary<string, SeedEntry> _translations;
    private readonly object _gate = new();

    public TranslationSeedStore(IBootstrapDataProvider bootstrapDataProvider)
    {
        _translations = new ConcurrentDictionary<string, SeedEntry>(
            bootstrapDataProvider
                .GetTranslations()
                .Select(item => new KeyValuePair<string, SeedEntry>(
                    item.Language,
                    new SeedEntry(CreateETag(item.Language), item.Payload))),
            StringComparer.OrdinalIgnoreCase);
    }

    public ResourceEnvelope<TranslationsResponse>? Get(string language)
    {
        return _translations.TryGetValue(language, out var translations)
            ? new ResourceEnvelope<TranslationsResponse>(translations.Resource, translations.ETag)
            : null;
    }

    public UpsertResult<TranslationsResponse> Upsert(string language, TranslationsResponse request, string? expectedETag)
    {
        lock (_gate)
        {
            _translations.TryGetValue(language, out var existing);
            OptimisticConcurrency.EnsureSatisfied(expectedETag, existing?.ETag, $"translations '{language}'");

            var created = existing is null;
            var entry = new SeedEntry(CreateETag(language), request);
            _translations[language] = entry;
            return new UpsertResult<TranslationsResponse>(request, created, ETag: entry.ETag);
        }
    }

    public void Store(string language, TranslationsResponse request, string? etag)
    {
        _translations[language] = new SeedEntry(etag ?? CreateETag(language), request);
    }

    public IReadOnlyCollection<KeyValuePair<string, TranslationsResponse>> GetAll()
    {
        return _translations
            .Select(item => new KeyValuePair<string, TranslationsResponse>(item.Key, item.Value.Resource))
            .ToArray();
    }

    private static string CreateETag(string language) => $"\"{language}-{Guid.NewGuid():N}\"";
}
