using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.TenantConfig;

public sealed record GetTenantConfigQuery(string? TenantId) : IRequest<ResourceEnvelope<TenantBootstrapResponse>?>;
