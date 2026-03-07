using MediatR;
using React_Receiver.Infrastructure.Inspections;

namespace React_Receiver.Application.Inspections;

public sealed class GetInspectionFileQueryHandler : IRequestHandler<GetInspectionFileQuery, InspectionFileStreamResult?>
{
    private readonly IInspectionApplicationService _inspectionApplicationService;

    public GetInspectionFileQueryHandler(IInspectionApplicationService inspectionApplicationService)
    {
        _inspectionApplicationService = inspectionApplicationService;
    }

    public Task<InspectionFileStreamResult?> Handle(GetInspectionFileQuery request, CancellationToken cancellationToken)
    {
        return _inspectionApplicationService.GetFileAsync(request.SessionId, request.FileName, cancellationToken);
    }
}
