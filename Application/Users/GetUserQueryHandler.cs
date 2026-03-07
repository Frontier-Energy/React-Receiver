using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Users;

public sealed class GetUserQueryHandler : IRequestHandler<GetUserQuery, GetUserResponse?>
{
    private readonly IUserApplicationService _userApplicationService;

    public GetUserQueryHandler(IUserApplicationService userApplicationService)
    {
        _userApplicationService = userApplicationService;
    }

    public Task<GetUserResponse?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        return _userApplicationService.GetUserAsync(request.UserId, cancellationToken);
    }
}
