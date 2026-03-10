using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed record GetInspectionIngestOutboxSessionQuery(string SessionId) : IRequest<InspectionIngestOutboxSessionDetail?>;
