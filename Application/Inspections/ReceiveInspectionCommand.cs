using MediatR;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed record ReceiveInspectionCommand(ReceiveInspectionFormRequest Request)
    : IRequest<ReceiveInspectionResponse>, ITransactionalRequest;
