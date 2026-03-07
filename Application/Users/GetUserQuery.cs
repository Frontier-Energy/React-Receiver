using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Users;

public sealed record GetUserQuery(string UserId) : IRequest<GetUserResponse?>;
