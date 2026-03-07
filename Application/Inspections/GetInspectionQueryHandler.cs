using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class GetInspectionQueryHandler : IRequestHandler<GetInspectionQuery, GetInspectionResponse?>
{
    private readonly IInspectionApplicationService _inspectionApplicationService;

    public GetInspectionQueryHandler(IInspectionApplicationService inspectionApplicationService)
    {
        _inspectionApplicationService = inspectionApplicationService;
    }

    public Task<GetInspectionResponse?> Handle(GetInspectionQuery request, CancellationToken cancellationToken)
    {
        return _inspectionApplicationService.GetInspectionAsync(request.SessionId, cancellationToken);
    }
}
