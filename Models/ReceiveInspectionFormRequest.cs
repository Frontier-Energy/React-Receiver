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

    public static bool TryValidateFileSignature(
        string? fileName,
        string? declaredContentType,
        ReadOnlySpan<byte> content,
        out string detectedContentType,
        out string errorMessage)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        detectedContentType = string.Empty;

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            errorMessage = "Only .jpg, .jpeg, .png, .pdf, and .txt files are accepted.";
            return false;
        }

        if (!TryDetectContentType(extension, content, out detectedContentType))
        {
            errorMessage = $"File '{Path.GetFileName(fileName)}' content does not match the expected {extension} file signature.";
            return false;
        }

        if (!AllowedContentTypesByExtension.TryGetValue(extension, out var allowedTypes) ||
            !allowedTypes.Contains(detectedContentType))
        {
            errorMessage = $"File '{Path.GetFileName(fileName)}' content was detected as '{detectedContentType}', which is not allowed for {extension}.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(declaredContentType) && !allowedTypes.Contains(declaredContentType))
        {
            errorMessage =
                $"File '{Path.GetFileName(fileName)}' declared content type '{declaredContentType}' does not match detected content '{detectedContentType}'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryDetectContentType(string extension, ReadOnlySpan<byte> content, out string contentType)
    {
        contentType = string.Empty;
        switch (extension.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                if (content.Length >= 3 &&
                    content[0] == 0xFF &&
                    content[1] == 0xD8 &&
                    content[2] == 0xFF)
                {
                    contentType = "image/jpeg";
                    return true;
                }

                return false;
            case ".png":
                if (content.Length >= 8 &&
                    content[0] == 0x89 &&
                    content[1] == 0x50 &&
                    content[2] == 0x4E &&
                    content[3] == 0x47 &&
                    content[4] == 0x0D &&
                    content[5] == 0x0A &&
                    content[6] == 0x1A &&
                    content[7] == 0x0A)
                {
                    contentType = "image/png";
                    return true;
                }

                return false;
            case ".pdf":
                if (content.Length >= 5 &&
                    content[0] == 0x25 &&
                    content[1] == 0x50 &&
                    content[2] == 0x44 &&
                    content[3] == 0x46 &&
                    content[4] == 0x2D)
                {
                    contentType = "application/pdf";
                    return true;
                }

                return false;
            case ".txt":
                if (LooksLikeUtf8Text(content))
                {
                    contentType = "text/plain";
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private static bool LooksLikeUtf8Text(ReadOnlySpan<byte> content)
    {
        if (content.IsEmpty)
        {
            return false;
        }

        if (content.IndexOf((byte)0x00) >= 0)
        {
            return false;
        }

        try
        {
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(content);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
