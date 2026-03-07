namespace React_Receiver.Infrastructure.Inspections;

public sealed record InspectionFileStreamResult(
    Stream Content,
    string ContentType,
    string FileName
);
