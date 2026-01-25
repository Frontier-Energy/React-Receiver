using Microsoft.AspNetCore.Http;
using React_Receiver.Handlers;
using Xunit;

namespace React_Receiver.Tests;

public sealed class ReceiveInspectionRequestParserTests
{
    [Fact]
    public void TryParseFormRequest_ReturnsTrue_WithEmptyPayload()
    {
        var parser = new ReceiveInspectionRequestParser();

        var result = parser.TryParseFormRequest(null, null, out var request);

        Assert.True(result);
        Assert.Null(request.SessionId);
        Assert.Null(request.UserId);
        Assert.Null(request.Name);
        Assert.Null(request.QueryParams);
        Assert.Null(request.Files);
    }

    [Fact]
    public void TryParseFormRequest_ReturnsFalse_WithInvalidPayload()
    {
        var parser = new ReceiveInspectionRequestParser();

        var result = parser.TryParseFormRequest("not json", null, out var request);

        Assert.False(result);
        Assert.Null(request.SessionId);
        Assert.Null(request.UserId);
        Assert.Null(request.Name);
    }

    [Fact]
    public void TryParseFormRequest_UnwrapsQuotedJson()
    {
        var parser = new ReceiveInspectionRequestParser();
        var payload = "\"{\\\"sessionId\\\":\\\"session-1\\\",\\\"name\\\":\\\"Sample\\\"}\"";
        IFormFile[] files = Array.Empty<IFormFile>();

        var result = parser.TryParseFormRequest(payload, files, out var request);

        Assert.True(result);
        Assert.Equal("session-1", request.SessionId);
        Assert.Equal("Sample", request.Name);
        Assert.Same(files, request.Files);
    }
}
