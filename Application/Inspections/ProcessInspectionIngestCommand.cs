using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed record ProcessInspectionIngestCommand(string SessionId) : IRequest<InspectionIngestProcessResult>;
