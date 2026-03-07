using MediatR;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Models;

namespace React_Receiver.Application.Translations;

public sealed record UpsertTranslationsCommand(string Language, TranslationsResponse Request)
    : IRequest<UpsertResult<TranslationsResponse>>, ITransactionalRequest;
