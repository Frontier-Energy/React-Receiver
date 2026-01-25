using React_Receiver.Models;

namespace React_Receiver.Handlers;

public interface ILoginRequestHandler
{
    LoginRequestResponse HandleLogin(LoginRequestCommand request);
}

public sealed class LoginRequestHandler : ILoginRequestHandler
{
    public LoginRequestResponse HandleLogin(LoginRequestCommand request)
    {
        return new LoginRequestResponse(UserId: Guid.NewGuid().ToString("N"));
    }
}
