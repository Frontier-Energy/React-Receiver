using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed record ReplayInspectionIngestOutboxSessionCommand(string SessionId, bool Force) : IRequest<ReplayInspectionIngestSessionResponse>;
