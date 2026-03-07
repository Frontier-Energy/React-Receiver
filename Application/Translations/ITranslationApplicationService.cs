using React_Receiver.Models;

namespace React_Receiver.Application.Translations;

public interface ITranslationApplicationService
{
    Task<ResourceEnvelope<TranslationsResponse>?> GetAsync(string language, CancellationToken cancellationToken);
    Task<UpsertResult<TranslationsResponse>> UpsertAsync(string language, TranslationsResponse request, string? expectedETag, CancellationToken cancellationToken);
    Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken);
}
