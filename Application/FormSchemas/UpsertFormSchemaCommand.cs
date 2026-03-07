using MediatR;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Models;

namespace React_Receiver.Application.FormSchemas;

public sealed record UpsertFormSchemaCommand(string FormType, FormSchemaResponse Request)
    : IRequest<UpsertResult<FormSchemaResponse>>, ITransactionalRequest;
