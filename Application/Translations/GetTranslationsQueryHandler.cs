using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Translations;

public sealed class GetTranslationsQueryHandler : IRequestHandler<GetTranslationsQuery, TranslationsResponse?>
{
    private readonly ITranslationApplicationService _translationApplicationService;

    public GetTranslationsQueryHandler(ITranslationApplicationService translationApplicationService)
    {
        _translationApplicationService = translationApplicationService;
    }

    public Task<TranslationsResponse?> Handle(GetTranslationsQuery request, CancellationToken cancellationToken)
    {
        return _translationApplicationService.GetAsync(request.Language, cancellationToken);
    }
}
