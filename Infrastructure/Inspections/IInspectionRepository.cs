using React_Receiver.Models;

namespace React_Receiver.Infrastructure.Inspections;

public interface IInspectionRepository
{
    Task<ReceiveInspectionResponse> PrepareAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken);
    Task<InspectionIngestProcessResult> ProcessPendingAsync(string sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetPendingSessionIdsAsync(int maxResults, CancellationToken cancellationToken);
    Task<GetInspectionResponse?> GetAsync(string sessionId, CancellationToken cancellationToken);
    Task<InspectionFileStreamResult?> GetFileAsync(string sessionId, string fileName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<InspectionIngestOutboxSessionSummary>> GetOutboxSessionsAsync(string? status, int limit, CancellationToken cancellationToken);
    Task<InspectionIngestOutboxSessionDetail?> GetOutboxSessionAsync(string sessionId, CancellationToken cancellationToken);
    Task<ReplayInspectionIngestSessionResponse> ReplayOutboxSessionAsync(string sessionId, bool force, CancellationToken cancellationToken);
}
