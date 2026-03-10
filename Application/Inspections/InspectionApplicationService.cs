using MediatR;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class InspectionApplicationService : IInspectionApplicationService
{
    private readonly IInspectionRepository _inspectionRepository;
    private readonly ISender _sender;
    private readonly ILogger<InspectionApplicationService> _logger;

    public InspectionApplicationService(
        IInspectionRepository inspectionRepository,
        ISender sender,
        ILogger<InspectionApplicationService> logger)
    {
        _inspectionRepository = inspectionRepository;
        _sender = sender;
        _logger = logger;
    }

    public async Task<ReceiveInspectionResponse> ReceiveInspectionAsync(
        ReceiveInspectionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _inspectionRepository.PrepareAsync(request, cancellationToken);

        try
        {
            var result = await _sender.Send(new ProcessInspectionIngestCommand(response.SessionId), cancellationToken);
            if (result.TerminalFailure)
            {
                _logger.LogError(
                    "Inspection ingest session {SessionId} entered terminal state {Status}: {LastError}",
                    response.SessionId,
                    result.Status,
                    result.LastError);
            }
            else if (!result.Processed)
            {
                _logger.LogWarning(
                    "Inspection ingest finalization will continue asynchronously for session {SessionId}. Current status: {Status}",
                    response.SessionId,
                    result.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Inspection ingest finalization will be retried asynchronously for session {SessionId}",
                response.SessionId);
        }

        return response;
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
