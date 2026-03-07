using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Translations;

public sealed record GetTranslationsQuery(string Language) : IRequest<ResourceEnvelope<TranslationsResponse>?>;
