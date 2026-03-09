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
        var repository = new FakeUserRepository();
        var handler = CreateHandler(repository);
        var request = new RegisterRequestModel("new@example.com", "A", "B");

        var result = await handler.RegisterAsync(request, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.UserId));
        Assert.Equal(0, repository.FindByEmailCalls);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsExistingUserId_WhenEmailAlreadyRegistered()
    {
        var repository = new FakeUserRepository();
        var handler = CreateHandler(repository);
        var request = new RegisterRequestModel("a@example.com", "A", "B");

        var result = await handler.RegisterAsync(request, CancellationToken.None);

        Assert.Equal("existing-user", result.UserId);
        Assert.Equal(0, repository.FindByEmailCalls);
    }

    private static AuthApplicationService CreateHandler(FakeUserRepository? repository = null)
    {
        return new AuthApplicationService(repository ?? new FakeUserRepository());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public int FindByEmailCalls { get; private set; }

        public Task<UserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<UserProfile?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        {
            FindByEmailCalls++;
            var user = string.Equals(email, "a@example.com", StringComparison.OrdinalIgnoreCase)
                ? new UserProfile("existing-user", email, "A", "B")
                : null;
            return Task.FromResult(user);
        }

        public Task<UserProfile> GetOrAddByEmailAsync(UserProfile user, CancellationToken cancellationToken)
        {
            if (string.Equals(user.Email, "a@example.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new UserProfile("existing-user", user.Email, "A", "B"));
            }

            return Task.FromResult(user);
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
