using React_Receiver.Domain.Users;
using React_Receiver.Infrastructure.Users;
using React_Receiver.Models;

namespace React_Receiver.Application.Users;

public sealed class UserApplicationService : IUserApplicationService
{
    private static readonly CurrentUser DefaultCurrentUser = new(
        "a1b2c3",
        ["admin"],
        ["tenant.select", "customization.admin"]);

    private readonly IUserRepository _userRepository;

    public UserApplicationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? null
            : new GetUserResponse(new UserModel(user.UserId, user.Email, user.FirstName, user.LastName));
    }

    public async Task<MeResponse> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var currentUser = await _userRepository.GetCurrentUserAsync(cancellationToken);
        if (currentUser is not null)
        {
            return new MeResponse(currentUser.UserId, currentUser.Roles, currentUser.Permissions);
        }

        await _userRepository.SaveCurrentUserAsync(DefaultCurrentUser, cancellationToken);
        return new MeResponse(DefaultCurrentUser.UserId, DefaultCurrentUser.Roles, DefaultCurrentUser.Permissions);
    }
}
