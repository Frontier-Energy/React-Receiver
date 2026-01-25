using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using React_Receiver.Controllers;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class ReceiveInspectionTests
{
    [Fact]
    public async Task ReceiveInspection_ReturnsBadRequest_WhenPayloadInvalid()
    {
        var handler = new FakeInspectionRequestHandler();
        var controller = CreateController(handler);

        var result = await controller.ReceiveInspection("not json", null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid payload JSON.", badRequest.Value);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ReceiveInspection_CallsHandlerAndReturnsOk_WhenPayloadMissing()
    {
        var handler = new FakeInspectionRequestHandler();
        var controller = CreateController(handler);

        var result = await controller.ReceiveInspection(null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ReceiveInspectionResponse>(ok.Value);
        Assert.Equal("Received", response.Status);
        Assert.Equal(string.Empty, response.SessionId);
        Assert.Equal(string.Empty, response.Name);
        Assert.Empty(response.QueryParams);
        Assert.Equal(1, handler.CallCount);
        Assert.NotNull(handler.LastRequest);
        Assert.Null(handler.LastRequest?.Files);
    }

    [Fact]
    public async Task ReceiveInspection_UsesParsedPayloadAndFiles()
    {
        var handler = new FakeInspectionRequestHandler();
        var controller = CreateController(handler);
        var files = new[] { CreateFormFile("inspection.txt", "hello") };
        var payload = "{\"sessionId\":\"session-1\",\"userId\":\"user-1\",\"name\":\"Sample\",\"queryParams\":{\"a\":\"b\"}}";

        var result = await controller.ReceiveInspection(payload, files);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ReceiveInspectionResponse>(ok.Value);
        Assert.Equal("session-1", response.SessionId);
        Assert.Equal("Sample", response.Name);
        Assert.Equal("b", response.QueryParams["a"]);
        Assert.Equal(1, handler.CallCount);
        Assert.Same(files, handler.LastRequest?.Files);
        Assert.Equal("session-1", handler.LastRequest?.SessionId);
    }

    private static QHVACController CreateController(FakeInspectionRequestHandler handler)
    {
        var tableClient = new TableServiceClient("UseDevelopmentStorage=true");
        var options = Options.Create(new TableStorageOptions { ConnectionString = string.Empty });
        var controller = new QHVACController(handler, tableClient, options)
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

    private sealed class FakeInspectionRequestHandler : IInspectionRequestHandler
    {
        public int CallCount { get; private set; }
        public ReceiveInspectionRequest? LastRequest { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task SaveRequestAsync(ReceiveInspectionRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
