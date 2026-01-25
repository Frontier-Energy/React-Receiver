namespace React_Receiver.Models;

public sealed record InspectionFileReference(
    string FileName,
    string SessionId,
    string FileType
);
