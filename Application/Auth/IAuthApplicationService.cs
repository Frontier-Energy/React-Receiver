using React_Receiver.Models;

namespace React_Receiver.Application.Auth;

public interface IAuthApplicationService
{
    Task<LoginRequestResponse> LoginAsync(LoginRequestCommand request, CancellationToken cancellationToken);
    Task<RegisterResponseModel> RegisterAsync(RegisterRequestModel request, CancellationToken cancellationToken);
}
