using MediatR;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class ProcessInspectionIngestCommandHandler : IRequestHandler<ProcessInspectionIngestCommand, InspectionIngestProcessResult>
{
    private readonly IInspectionRepository _inspectionRepository;

    public ProcessInspectionIngestCommandHandler(IInspectionRepository inspectionRepository)
    {
        _inspectionRepository = inspectionRepository;
    }

    public Task<InspectionIngestProcessResult> Handle(ProcessInspectionIngestCommand request, CancellationToken cancellationToken)
    {
        return _inspectionRepository.ProcessPendingAsync(request.SessionId, cancellationToken);
    }
}
