using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using React_Receiver.Application.Inspections;
using React_Receiver.Controllers;
using React_Receiver.Handlers;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Models;
using React_Receiver.Tests.TestDoubles;
using Xunit;

namespace React_Receiver.Tests;

public sealed class ReceiveInspectionTests
{
    [Fact]
    public async Task ReceiveInspectionHandler_Throws_WhenPayloadInvalid()
    {
        var handler = new FakeInspectionRequestHandler();
        var commandHandler = new ReceiveInspectionCommandHandler(handler, new ReceiveInspectionRequestParser());

        var exception = await Assert.ThrowsAsync<RequestParsingException>(() =>
            commandHandler.Handle(new ReceiveInspectionCommand(new ReceiveInspectionFormRequest("not json", null)), CancellationToken.None));

        Assert.Equal("Payload must be valid JSON.", exception.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ReceiveInspection_CallsMediatorAndReturnsOk_WhenPayloadMissing()
    {
        ReceiveInspectionCommand? capturedCommand = null;
        var controller = CreateController((request, _) =>
        {
            var command = Assert.IsType<ReceiveInspectionCommand>(request);
            capturedCommand = command;
            return Task.FromResult<object?>(new ReceiveInspectionResponse(
                Status: "Received",
                SessionId: string.Empty,
                Name: string.Empty,
                QueryParams: new Dictionary<string, string>(),
                Message: "OK"));
        });

        var result = await controller.ReceiveInspection(new ReceiveInspectionFormRequest(null, null));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ReceiveInspectionResponse>(ok.Value);
        Assert.Equal("Received", response.Status);
        Assert.Equal(string.Empty, response.SessionId);
        Assert.Equal(string.Empty, response.Name);
        Assert.Empty(response.QueryParams);
        Assert.NotNull(capturedCommand);
        Assert.Null(capturedCommand!.Request.Files);
    }

    [Fact]
    public async Task ReceiveInspectionHandler_UsesParsedPayloadAndFiles()
    {
        var handler = new FakeInspectionRequestHandler();
        var commandHandler = new ReceiveInspectionCommandHandler(handler, new ReceiveInspectionRequestParser());
        var files = new[] { CreateFormFile("inspection.txt", "hello") };
        var payload = "{\"sessionId\":\"session-1\",\"userId\":\"user-1\",\"name\":\"Sample\",\"queryParams\":{\"a\":\"b\"}}";

        var response = await commandHandler.Handle(
            new ReceiveInspectionCommand(new ReceiveInspectionFormRequest(payload, files)),
            CancellationToken.None);

        Assert.Equal("session-1", response.SessionId);
        Assert.Equal("Sample", response.Name);
        Assert.Equal("b", response.QueryParams["a"]);
        Assert.Equal(1, handler.CallCount);
        Assert.Same(files, handler.LastRequest?.Files);
        Assert.Equal("session-1", handler.LastRequest?.SessionId);
    }

    private static InspectionsController CreateController(Func<object, CancellationToken, Task<object?>> handler)
    {
        var controller = new InspectionsController(new TestSender(handler), NullLogger<InspectionsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static IFormFile CreateFormFile(string fileName, string contents)
    {
        var bytes = Encoding.UTF8.GetBytes(contents);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "files", fileName);
    }

    private sealed class FakeInspectionRequestHandler : IInspectionApplicationService
    {
        public int CallCount { get; private set; }
        public ReceiveInspectionRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<ReceiveInspectionResponse> ReceiveInspectionAsync(
            ReceiveInspectionRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastToken = cancellationToken;
            var response = new ReceiveInspectionResponse(
                Status: "Received",
                SessionId: request.SessionId ?? string.Empty,
                Name: request.Name ?? string.Empty,
                QueryParams: request.QueryParams ?? new Dictionary<string, string>(),
            Message: "OK");
            return Task.FromResult(response);
        }

        public Task<GetInspectionResponse?> GetInspectionAsync(string sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<React_Receiver.Infrastructure.Inspections.InspectionFileStreamResult?> GetFileAsync(string sessionId, string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
