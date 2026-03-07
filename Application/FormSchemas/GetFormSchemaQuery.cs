using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.FormSchemas;

public sealed record GetFormSchemaQuery(string FormType) : IRequest<ResourceEnvelope<FormSchemaResponse>?>;
