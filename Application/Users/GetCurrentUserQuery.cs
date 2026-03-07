using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Users;

public sealed record GetCurrentUserQuery : IRequest<MeResponse>;
