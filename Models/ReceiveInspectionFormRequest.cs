using System.Collections.Frozen;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace React_Receiver.Models;

public sealed record ReceiveInspectionFormRequest(
    string? Payload,
    IFormFile[]? Files
)
{
    public const int MaxPayloadBytes = 64 * 1024;
    public const int MaxFileCount = 10;
    public const long MaxFileBytes = 10L * 1024 * 1024;
    public const long MaxTotalFileBytes = 25L * 1024 * 1024;
    public const long MaxMultipartBodyLengthBytes = MaxTotalFileBytes + MaxPayloadBytes + (1L * 1024 * 1024);

    private static readonly FrozenSet<string> AllowedExtensions = new[]
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".pdf",
        ".txt"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, FrozenSet<string>> AllowedContentTypesByExtension =
        new Dictionary<string, FrozenSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new[] { "image/jpeg", "image/jpg" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            [".jpeg"] = new[] { "image/jpeg", "image/jpg" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            [".png"] = new[] { "image/png" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            [".pdf"] = new[] { "application/pdf" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            [".txt"] = new[] { "text/plain" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static int GetPayloadSizeBytes(string? payload)
    {
        return string.IsNullOrEmpty(payload)
            ? 0
            : Encoding.UTF8.GetByteCount(payload);
    }

    public static bool TryValidateFileType(string? fileName, string? contentType, out string errorMessage)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            errorMessage = "Only .jpg, .jpeg, .png, .pdf, and .txt files are accepted.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            errorMessage = $"File '{Path.GetFileName(fileName)}' must declare a content type.";
            return false;
        }

        if (!AllowedContentTypesByExtension.TryGetValue(extension, out var allowedTypes) ||
            !allowedTypes.Contains(contentType))
        {
            errorMessage =
                $"File '{Path.GetFileName(fileName)}' must use an allowed MIME type for {extension}: {string.Join(", ", allowedTypes ?? [])}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
