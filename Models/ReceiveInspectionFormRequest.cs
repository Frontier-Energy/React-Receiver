using Microsoft.AspNetCore.Http;

namespace React_Receiver.Models;

public sealed record ReceiveInspectionFormRequest(
    string? Payload,
    IFormFile[]? Files
);
