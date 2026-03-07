using MediatR;

namespace React_Receiver.Application.Inspections;

public sealed record ProcessInspectionIngestCommand(string SessionId) : IRequest<bool>;
