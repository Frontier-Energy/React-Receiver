using MediatR;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Models;

namespace React_Receiver.Application.Auth;

public sealed record RegisterCommand(RegisterRequestModel Request)
    : IRequest<RegisterResponseModel>, ITransactionalRequest;
