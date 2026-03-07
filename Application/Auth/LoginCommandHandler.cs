using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Auth;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginRequestResponse>
{
    private readonly IAuthApplicationService _authApplicationService;

    public LoginCommandHandler(IAuthApplicationService authApplicationService)
    {
        _authApplicationService = authApplicationService;
    }

    public Task<LoginRequestResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        return _authApplicationService.LoginAsync(request.Request, cancellationToken);
    }
}
