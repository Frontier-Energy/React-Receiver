using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Users;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, MeResponse>
{
    private readonly IUserApplicationService _userApplicationService;

    public GetCurrentUserQueryHandler(IUserApplicationService userApplicationService)
    {
        _userApplicationService = userApplicationService;
    }

    public Task<MeResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        return _userApplicationService.GetCurrentUserAsync(cancellationToken);
    }
}
