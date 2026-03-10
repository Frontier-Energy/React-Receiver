using MediatR;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class GetInspectionIngestOutboxSessionQueryHandler : IRequestHandler<GetInspectionIngestOutboxSessionQuery, InspectionIngestOutboxSessionDetail?>
{
    private readonly IInspectionRepository _inspectionRepository;

    public GetInspectionIngestOutboxSessionQueryHandler(IInspectionRepository inspectionRepository)
    {
        _inspectionRepository = inspectionRepository;
    }

    public Task<InspectionIngestOutboxSessionDetail?> Handle(
        GetInspectionIngestOutboxSessionQuery request,
        CancellationToken cancellationToken)
    {
        return _inspectionRepository.GetOutboxSessionAsync(request.SessionId, cancellationToken);
    }
}
