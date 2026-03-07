using React_Receiver.Models;

namespace React_Receiver.Application.Users;

public interface IUserApplicationService
{
    Task<GetUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken);
    Task<MeResponse> GetCurrentUserAsync(CancellationToken cancellationToken);
}
