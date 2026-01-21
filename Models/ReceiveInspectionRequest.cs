using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace React_Receiver.Models;

public sealed record ReceiveInspectionRequest(
    string? SessionId,
    string? UserId,
    string? Name,
    Dictionary<string, string>? QueryParams,
    IFormFile[]? Files
);
