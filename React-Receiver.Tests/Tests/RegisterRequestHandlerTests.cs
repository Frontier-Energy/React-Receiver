using System.Threading;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using React_Receiver.Handlers;
using React_Receiver.Models;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class RegisterRequestHandlerTests
{
    [Fact]
    public async Task HandleRegisterAsync_ReturnsSameUserId_WhenConnectionStringEmpty_WithEmail()
    {
        var handler = CreateHandler();
        var request = new RegisterRequestModel("a@example.com", "A", "B", null);

        var result = await handler.HandleRegisterAsync(request, "user-1", CancellationToken.None);

        Assert.Equal("user-1", result);
    }

    [Fact]
    public async Task HandleRegisterAsync_ReturnsSameUserId_WhenConnectionStringEmpty_WithoutEmail()
    {
        var handler = CreateHandler();
        var request = new RegisterRequestModel(null, "A", "B", null);

        var result = await handler.HandleRegisterAsync(request, "user-2", CancellationToken.None);

        Assert.Equal("user-2", result);
    }

    private static RegisterRequestHandler CreateHandler()
    {
        var tableClient = new TableServiceClient("UseDevelopmentStorage=true");
        var options = Options.Create(new TableStorageOptions { ConnectionString = string.Empty });
        return new RegisterRequestHandler(tableClient, options);
    }
}
