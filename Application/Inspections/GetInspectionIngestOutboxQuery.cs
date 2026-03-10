using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed record GetInspectionIngestOutboxQuery(string? Status, int Limit) : IRequest<IReadOnlyCollection<InspectionIngestOutboxSessionSummary>>;
