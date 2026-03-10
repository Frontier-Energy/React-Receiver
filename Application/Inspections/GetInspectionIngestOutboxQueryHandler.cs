using MediatR;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class GetInspectionIngestOutboxQueryHandler : IRequestHandler<GetInspectionIngestOutboxQuery, IReadOnlyCollection<InspectionIngestOutboxSessionSummary>>
{
    private readonly IInspectionRepository _inspectionRepository;

    public GetInspectionIngestOutboxQueryHandler(IInspectionRepository inspectionRepository)
    {
        _inspectionRepository = inspectionRepository;
    }

    public Task<IReadOnlyCollection<InspectionIngestOutboxSessionSummary>> Handle(
        GetInspectionIngestOutboxQuery request,
        CancellationToken cancellationToken)
    {
        return _inspectionRepository.GetOutboxSessionsAsync(request.Status, request.Limit, cancellationToken);
    }
}
