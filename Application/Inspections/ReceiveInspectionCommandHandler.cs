using MediatR;
using React_Receiver.Handlers;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Models;

namespace React_Receiver.Application.Inspections;

public sealed class ReceiveInspectionCommandHandler : IRequestHandler<ReceiveInspectionCommand, ReceiveInspectionResponse>
{
    private readonly IInspectionApplicationService _inspectionApplicationService;
    private readonly IReceiveInspectionRequestParser _receiveInspectionRequestParser;

    public ReceiveInspectionCommandHandler(
        IInspectionApplicationService inspectionApplicationService,
        IReceiveInspectionRequestParser receiveInspectionRequestParser)
    {
        _inspectionApplicationService = inspectionApplicationService;
        _receiveInspectionRequestParser = receiveInspectionRequestParser;
    }

    public Task<ReceiveInspectionResponse> Handle(ReceiveInspectionCommand request, CancellationToken cancellationToken)
    {
        if (!_receiveInspectionRequestParser.TryParseFormRequest(
            request.Request.Payload,
            request.Request.Files,
            out var parsedRequest,
            out var errorMessage))
        {
            throw new RequestParsingException(errorMessage ?? "Payload must be valid JSON.");
        }

        return _inspectionApplicationService.ReceiveInspectionAsync(parsedRequest, cancellationToken);
    }
}
