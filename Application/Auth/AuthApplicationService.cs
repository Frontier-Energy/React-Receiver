using React_Receiver.Domain.Users;
using React_Receiver.Infrastructure.Users;
using React_Receiver.Models;

namespace React_Receiver.Application.Auth;

public sealed class AuthApplicationService : IAuthApplicationService
{
    private readonly IUserRepository _userRepository;

    public AuthApplicationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<LoginRequestResponse> LoginAsync(LoginRequestCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new LoginRequestResponse(string.Empty);
        }

        var user = await _userRepository.FindByEmailAsync(request.Email, cancellationToken);
        return new LoginRequestResponse(user?.UserId ?? string.Empty);
    }

    public async Task<RegisterResponseModel> RegisterAsync(RegisterRequestModel request, CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid().ToString("N");
        var user = await _userRepository.GetOrAddByEmailAsync(
            new UserProfile(userId, request.Email, request.FirstName, request.LastName),
            cancellationToken);
        return new RegisterResponseModel(user.UserId);
    }
}
