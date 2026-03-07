using MediatR;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Models;

namespace React_Receiver.Application.TenantConfig;

public sealed record UpsertTenantConfigCommand(TenantBootstrapResponse Request)
    : IRequest<TenantBootstrapResponse>, ITransactionalRequest;
