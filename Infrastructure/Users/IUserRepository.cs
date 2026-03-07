using React_Receiver.Domain.Users;

namespace React_Receiver.Infrastructure.Users;

public interface IUserRepository
{
    Task<UserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken);
    Task<UserProfile?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task AddAsync(UserProfile user, CancellationToken cancellationToken);
    Task<CurrentUser?> GetCurrentUserAsync(CancellationToken cancellationToken);
    Task SaveCurrentUserAsync(CurrentUser user, CancellationToken cancellationToken);
}
