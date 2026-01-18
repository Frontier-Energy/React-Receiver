using System.Collections.Generic;

namespace React_Receiver.Models;

public sealed record ReceiveInspectionRequest(
    string? SessionId,
    string? Name,
    Dictionary<string, string>? QueryParams
);
