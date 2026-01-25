namespace React_Receiver.Models;

public sealed record GetInspectionResponse(
    string SessionId,
    string? UserId,
    string? Name,
    Dictionary<string, string> QueryParams,
    InspectionFileReference[] Files
);
