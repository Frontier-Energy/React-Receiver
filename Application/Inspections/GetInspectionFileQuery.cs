using MediatR;
using React_Receiver.Infrastructure.Inspections;

namespace React_Receiver.Application.Inspections;

public sealed record GetInspectionFileQuery(string SessionId, string FileName) : IRequest<InspectionFileStreamResult?>;
