using System.Threading;
using System.Threading.Tasks;
using React_Receiver.Application.Auth;
using React_Receiver.Domain.Users;
using React_Receiver.Models;
using React_Receiver.Infrastructure.Users;
using Xunit;

namespace React_Receiver.Tests;

public sealed class RegisterRequestHandlerTests
{
    [Fact]
    public async Task RegisterAsync_GeneratesUserId_WhenNoExistingUserFound()
    {
        var handler = CreateHandler();
        var request = new RegisterRequestModel("new@example.com", "A", "B");

        var result = await handler.RegisterAsync(request, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.UserId));
    }

    [Fact]
    public async Task RegisterAsync_ReturnsExistingUserId_WhenEmailAlreadyRegistered()
    {
        var handler = CreateHandler();
        var request = new RegisterRequestModel("a@example.com", "A", "B");

        var result = await handler.RegisterAsync(request, CancellationToken.None);

        Assert.Equal("existing-user", result.UserId);
    }

    private static AuthApplicationService CreateHandler()
    {
        return new AuthApplicationService(new FakeUserRepository());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public Task<UserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<UserProfile?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            var user = string.Equals(email, "a@example.com", StringComparison.OrdinalIgnoreCase)
                ? new UserProfile("existing-user", email, "A", "B")
                : null;
            return Task.FromResult(user);
        }

        public Task AddAsync(UserProfile user, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<CurrentUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SaveCurrentUserAsync(CurrentUser user, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
