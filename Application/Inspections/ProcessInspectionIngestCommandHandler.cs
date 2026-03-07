using MediatR;
using React_Receiver.Infrastructure.Inspections;

namespace React_Receiver.Application.Inspections;

public sealed class ProcessInspectionIngestCommandHandler : IRequestHandler<ProcessInspectionIngestCommand, bool>
{
    private readonly IInspectionRepository _inspectionRepository;

    public ProcessInspectionIngestCommandHandler(IInspectionRepository inspectionRepository)
    {
        _inspectionRepository = inspectionRepository;
    }

    public Task<bool> Handle(ProcessInspectionIngestCommand request, CancellationToken cancellationToken)
    {
        return _inspectionRepository.ProcessPendingAsync(request.SessionId, cancellationToken);
    }
}
