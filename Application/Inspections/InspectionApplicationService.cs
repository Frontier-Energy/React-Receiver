using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class InspectionApplicationService : IInspectionApplicationService
{
    private readonly IInspectionRepository _inspectionRepository;

    public InspectionApplicationService(IInspectionRepository inspectionRepository)
    {
        _inspectionRepository = inspectionRepository;
    }

    public Task<ReceiveInspectionResponse> ReceiveInspectionAsync(
        ReceiveInspectionRequest request,
        CancellationToken cancellationToken)
    {
        return _inspectionRepository.SaveAsync(request, cancellationToken);
    }

    public Task<GetInspectionResponse?> GetInspectionAsync(string sessionId, CancellationToken cancellationToken)
    {
        return _inspectionRepository.GetAsync(sessionId, cancellationToken);
    }

    public Task<InspectionFileStreamResult?> GetFileAsync(
        string sessionId,
        string fileName,
        CancellationToken cancellationToken)
    {
        return _inspectionRepository.GetFileAsync(sessionId, fileName, cancellationToken);
    }
}
