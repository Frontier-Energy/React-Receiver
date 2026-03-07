using React_Receiver.Models;

namespace React_Receiver.Infrastructure.Inspections;

public interface IInspectionRepository
{
    Task<ReceiveInspectionResponse> SaveAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken);
    Task<GetInspectionResponse?> GetAsync(string sessionId, CancellationToken cancellationToken);
    Task<InspectionFileStreamResult?> GetFileAsync(string sessionId, string fileName, CancellationToken cancellationToken);
}
