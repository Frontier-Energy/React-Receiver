using System.Collections.Concurrent;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.Translations;

public interface ITranslationSeedStore
{
    TranslationsResponse? Get(string language);
    UpsertResult<TranslationsResponse> Upsert(string language, TranslationsResponse request);
    IReadOnlyCollection<KeyValuePair<string, TranslationsResponse>> GetAll();
}

public sealed class TranslationSeedStore : ITranslationSeedStore
{
    private readonly ConcurrentDictionary<string, TranslationsResponse> _translations;

    public TranslationSeedStore(IBootstrapDataProvider bootstrapDataProvider)
    {
        _translations = new ConcurrentDictionary<string, TranslationsResponse>(
            bootstrapDataProvider
                .GetTranslations()
                .Select(item => new KeyValuePair<string, TranslationsResponse>(item.Language, item.Payload)),
            StringComparer.OrdinalIgnoreCase);
    }

    public TranslationsResponse? Get(string language)
    {
        return _translations.TryGetValue(language, out var translations) ? translations : null;
    }

    public UpsertResult<TranslationsResponse> Upsert(string language, TranslationsResponse request)
    {
        var created = !_translations.ContainsKey(language);
        _translations[language] = request;
        return new UpsertResult<TranslationsResponse>(request, created);
    }

    public IReadOnlyCollection<KeyValuePair<string, TranslationsResponse>> GetAll()
    {
        return _translations.ToArray();
    }
}
