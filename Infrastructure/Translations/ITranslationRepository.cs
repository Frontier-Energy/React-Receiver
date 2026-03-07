using React_Receiver.Models;

namespace React_Receiver.Infrastructure.Translations;

public interface ITranslationRepository
{
    bool IsConfigured { get; }
    Task<ResourceEnvelope<TranslationsResponse>?> GetAsync(string language, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string language, CancellationToken cancellationToken);
    Task<UpsertResult<TranslationsResponse>> UpsertAsync(string language, TranslationsResponse request, string? expectedETag, CancellationToken cancellationToken);
}
