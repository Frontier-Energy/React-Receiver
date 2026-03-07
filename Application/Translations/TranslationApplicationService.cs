using React_Receiver.Infrastructure.Translations;
using React_Receiver.Models;

namespace React_Receiver.Application.Translations;

public sealed class TranslationApplicationService : ITranslationApplicationService
{
    private readonly ITranslationRepository _repository;
    private readonly ITranslationSeedStore _seedStore;

    public TranslationApplicationService(
        ITranslationRepository repository,
        ITranslationSeedStore seedStore)
    {
        _repository = repository;
        _seedStore = seedStore;
    }

    public Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return Task.FromResult(_seedStore.Get(language));
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
            return _seedStore.Upsert(language, request);
        }

        var response = await _repository.UpsertAsync(language, request, cancellationToken);
        _seedStore.Upsert(language, request);
        return response;
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return;
        }

        foreach (var translation in _seedStore.GetAll())
        {
            if (!overwriteExisting && await _repository.ExistsAsync(translation.Key, cancellationToken))
            {
                continue;
            }

            await _repository.UpsertAsync(translation.Key, translation.Value, cancellationToken);
        }
    }
}
