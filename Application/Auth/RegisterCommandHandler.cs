using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Auth;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResponseModel>
{
    private readonly IAuthApplicationService _authApplicationService;

    public RegisterCommandHandler(IAuthApplicationService authApplicationService)
    {
        _authApplicationService = authApplicationService;
    }

    public Task<RegisterResponseModel> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        return _authApplicationService.RegisterAsync(request.Request, cancellationToken);
    }
}
