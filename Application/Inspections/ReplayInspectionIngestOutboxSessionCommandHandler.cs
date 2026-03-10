using MediatR;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class ReplayInspectionIngestOutboxSessionCommandHandler : IRequestHandler<ReplayInspectionIngestOutboxSessionCommand, ReplayInspectionIngestSessionResponse>
{
    private readonly IInspectionRepository _inspectionRepository;
    private readonly ISender _sender;

    public ReplayInspectionIngestOutboxSessionCommandHandler(IInspectionRepository inspectionRepository, ISender sender)
    {
        _inspectionRepository = inspectionRepository;
        _sender = sender;
    }

    public async Task<ReplayInspectionIngestSessionResponse> Handle(
        ReplayInspectionIngestOutboxSessionCommand request,
        CancellationToken cancellationToken)
    {
        var replay = await _inspectionRepository.ReplayOutboxSessionAsync(
            request.SessionId,
            request.Force,
            cancellationToken);

        if (!replay.Accepted)
        {
            return replay;
        }

        var processResult = await _sender.Send(new ProcessInspectionIngestCommand(request.SessionId), cancellationToken);
        var session = await _inspectionRepository.GetOutboxSessionAsync(request.SessionId, cancellationToken);

        return new ReplayInspectionIngestSessionResponse(
            true,
            processResult.Processed
                ? "Replay processed successfully."
                : $"Replay submitted. Current status: {processResult.Status}.",
            session);
    }
}
