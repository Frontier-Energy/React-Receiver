using System.Collections.Concurrent;
using React_Receiver.Infrastructure.Translations;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.Translations;

public sealed class TranslationApplicationService : ITranslationApplicationService
{
    private readonly ITranslationRepository _repository;
    private readonly ConcurrentDictionary<string, TranslationsResponse> _translations;

    public TranslationApplicationService(
        ITranslationRepository repository,
        IBootstrapDataProvider bootstrapDataProvider)
    {
        _repository = repository;
        _translations = new ConcurrentDictionary<string, TranslationsResponse>(
            bootstrapDataProvider
                .GetTranslations()
                .Select(item => new KeyValuePair<string, TranslationsResponse>(item.Language, item.Payload)),
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return Task.FromResult(
                _translations.TryGetValue(language, out var translations)
                    ? translations
                    : null);
        }

        return _repository.GetAsync(language, cancellationToken);
    }

    public async Task<UpsertResult<TranslationsResponse>> UpsertAsync(
        string language,
        TranslationsResponse request,
        CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            var created = !_translations.ContainsKey(language);
            _translations[language] = request;
            return new UpsertResult<TranslationsResponse>(request, created);
        }

        var response = await _repository.UpsertAsync(language, request, cancellationToken);
        _translations[language] = request;
        return response;
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return;
        }

        foreach (var translation in _translations)
        {
            if (!overwriteExisting && await _repository.ExistsAsync(translation.Key, cancellationToken))
            {
                continue;
            }

            await _repository.UpsertAsync(translation.Key, translation.Value, cancellationToken);
        }
    }
}
