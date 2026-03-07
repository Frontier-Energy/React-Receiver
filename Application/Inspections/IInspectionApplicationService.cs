using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public interface IInspectionApplicationService
{
    Task<ReceiveInspectionResponse> ReceiveInspectionAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken);
    Task<GetInspectionResponse?> GetInspectionAsync(string sessionId, CancellationToken cancellationToken);
    Task<InspectionFileStreamResult?> GetFileAsync(string sessionId, string fileName, CancellationToken cancellationToken);
}
