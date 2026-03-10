using System.Security.Cryptography;
using System.Text;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using React_Receiver.Models;

namespace React_Receiver.Services;

public interface IInspectionFileMalwareScanner
{
    ValueTask<MalwareScanResult> ScanAsync(
        string fileName,
        string detectedContentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);
}

public interface IInspectionFileSecurityInspector
{
    Task<InspectionFileInspectionResult> InspectAsync(IFormFile file, CancellationToken cancellationToken);
}

public sealed record InspectionFileInspectionResult(
    BinaryData Content,
    string DetectedContentType,
    string Sha256,
    bool Accepted,
    string RejectionReason,
    string ScanEngine,
    string ScanDetails);

public readonly record struct MalwareScanResult(
    bool IsClean,
    string Engine,
    string Details)
{
    public static MalwareScanResult Clean(string engine, string details)
    {
        return new(true, engine, details);
    }

    public static MalwareScanResult Rejected(string engine, string details)
    {
        return new(false, engine, details);
    }
}

public sealed class InspectionFileSecurityInspector : IInspectionFileSecurityInspector
{
    private readonly IInspectionFileMalwareScanner _malwareScanner;

    public InspectionFileSecurityInspector(IInspectionFileMalwareScanner malwareScanner)
    {
        _malwareScanner = malwareScanner;
    }

    public async Task<InspectionFileInspectionResult> InspectAsync(IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = file.OpenReadStream();
        using var buffer = file.Length > 0 && file.Length <= int.MaxValue
            ? new MemoryStream((int)file.Length)
            : new MemoryStream();

        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var content = BinaryData.FromBytes(bytes);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        if (!ReceiveInspectionFormRequest.TryValidateFileSignature(
                file.FileName,
                file.ContentType,
                bytes,
                out var detectedContentType,
                out var errorMessage))
        {
            return new InspectionFileInspectionResult(
                content,
                string.IsNullOrWhiteSpace(detectedContentType) ? "application/octet-stream" : detectedContentType,
                sha256,
                false,
                errorMessage,
                "content-sniffer/v1",
                errorMessage);
        }

        var malwareScan = await _malwareScanner.ScanAsync(
            file.FileName,
            detectedContentType,
            bytes,
            cancellationToken);

        return malwareScan.IsClean
            ? new InspectionFileInspectionResult(
                content,
                detectedContentType,
                sha256,
                true,
                string.Empty,
                malwareScan.Engine,
                malwareScan.Details)
            : new InspectionFileInspectionResult(
                content,
                detectedContentType,
                sha256,
                false,
                $"File '{Path.GetFileName(file.FileName)}' was rejected by malware scanning: {malwareScan.Details}.",
                malwareScan.Engine,
                malwareScan.Details);
    }
}

public sealed class SignatureInspectionFileMalwareScanner : IInspectionFileMalwareScanner
{
    private const string EngineName = "signature-baseline/v1";
    private static readonly byte[] EicarSignature = Encoding.ASCII.GetBytes(
        "X5O!P%@AP[4\\PZX54(P^)7CC)7}$" +
        "EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*");

    public ValueTask<MalwareScanResult> ScanAsync(
        string fileName,
        string detectedContentType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (content.Span.IndexOf(EicarSignature) >= 0)
        {
            return ValueTask.FromResult(MalwareScanResult.Rejected(
                EngineName,
                "Matched the EICAR anti-malware test signature."));
        }

        if (detectedContentType == "text/plain" &&
            content.Length >= 2 &&
            content.Span[0] == 0x4D &&
            content.Span[1] == 0x5A)
        {
            return ValueTask.FromResult(MalwareScanResult.Rejected(
                EngineName,
                "Executable content was detected inside a text upload."));
        }

        return ValueTask.FromResult(MalwareScanResult.Clean(
            EngineName,
            "No known signatures matched."));
    }
}

public static class InspectionFileBlobMetadata
{
    public const string VerificationStatusKey = "verificationstatus";
    public const string OriginalContentTypeKey = "originalcontenttype";
    public const string DetectedContentTypeKey = "detectedcontenttype";
    public const string OriginalFileNameKey = "originalfilename";
    public const string Sha256Key = "sha256";
    public const string ScanEngineKey = "scanengine";
    public const string ScanDetailsKey = "scandetails";

    public const string PendingStatus = "pending";
    public const string RejectedStatus = "rejected";
    public const string VerifiedStatus = "verified";

    public static IDictionary<string, string> Create(
        string originalFileName,
        string originalContentType,
        string detectedContentType,
        string sha256,
        string verificationStatus,
        string scanEngine,
        string scanDetails)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [VerificationStatusKey] = verificationStatus,
            [OriginalFileNameKey] = Truncate(originalFileName, 256),
            [OriginalContentTypeKey] = Truncate(originalContentType, 128),
            [DetectedContentTypeKey] = Truncate(detectedContentType, 128),
            [Sha256Key] = sha256,
            [ScanEngineKey] = Truncate(scanEngine, 128),
            [ScanDetailsKey] = Truncate(scanDetails, 512)
        };
    }

    public static BlobHttpHeaders CreateHeaders(string contentType)
    {
        return new BlobHttpHeaders
        {
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}
