using MediatR;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Models;

namespace React_Receiver.Application.Translations;

public sealed record UpsertTranslationsCommand(string Language, TranslationsResponse Request, string? ExpectedETag)
    : IRequest<UpsertResult<TranslationsResponse>>, ITransactionalRequest;
