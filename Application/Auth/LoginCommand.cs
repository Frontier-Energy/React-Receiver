using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.Auth;

public sealed record LoginCommand(LoginRequestCommand Request) : IRequest<LoginRequestResponse>;
