using System.Text;
using Microsoft.AspNetCore.Http;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class InspectionFileSecurityServicesTests
{
    [Fact]
    public async Task InspectAsync_RejectsEicarPayloads()
    {
        var inspector = new InspectionFileSecurityInspector(new SignatureInspectionFileMalwareScanner());
        var file = CreateFormFile(
            "note.txt",
            "text/plain",
            Encoding.ASCII.GetBytes("X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*"));

        var result = await inspector.InspectAsync(file, CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Contains("malware scanning", result.RejectionReason);
        Assert.Equal("text/plain", result.DetectedContentType);
    }

    [Fact]
    public async Task InspectAsync_AcceptsValidPngPayloads()
    {
        var inspector = new InspectionFileSecurityInspector(new SignatureInspectionFileMalwareScanner());
        var file = CreateFormFile(
            "image.png",
            "image/png",
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);

        var result = await inspector.InspectAsync(file, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal("image/png", result.DetectedContentType);
        Assert.False(string.IsNullOrWhiteSpace(result.Sha256));
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
