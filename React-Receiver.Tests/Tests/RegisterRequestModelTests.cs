using React_Receiver.Models;
using Xunit;

namespace React_Receiver.Tests;

public sealed class RegisterRequestModelTests
{
    [Fact]
    public void PropertiesMatchConstructorArguments()
    {
        var model = new RegisterRequestModel("a@example.com", "A", "B", "user-1");

        Assert.Equal("a@example.com", model.Email);
        Assert.Equal("A", model.FirstName);
        Assert.Equal("B", model.LastName);
        Assert.Equal("user-1", model.UserId);
    }
}
