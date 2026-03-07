using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Translations;

public sealed class UpsertTranslationsCommandHandler : IRequestHandler<UpsertTranslationsCommand, UpsertResult<TranslationsResponse>>
{
    private readonly ITranslationApplicationService _translationApplicationService;

    public UpsertTranslationsCommandHandler(ITranslationApplicationService translationApplicationService)
    {
        _translationApplicationService = translationApplicationService;
    }

    public Task<UpsertResult<TranslationsResponse>> Handle(UpsertTranslationsCommand request, CancellationToken cancellationToken)
    {
        return _translationApplicationService.UpsertAsync(request.Language, request.Request, cancellationToken);
    }
}
