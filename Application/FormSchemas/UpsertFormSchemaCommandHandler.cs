using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.FormSchemas;

public sealed class UpsertFormSchemaCommandHandler : IRequestHandler<UpsertFormSchemaCommand, UpsertResult<FormSchemaResponse>>
{
    private readonly IFormSchemaApplicationService _formSchemaApplicationService;

    public UpsertFormSchemaCommandHandler(IFormSchemaApplicationService formSchemaApplicationService)
    {
        _formSchemaApplicationService = formSchemaApplicationService;
    }

    public Task<UpsertResult<FormSchemaResponse>> Handle(UpsertFormSchemaCommand request, CancellationToken cancellationToken)
    {
        return _formSchemaApplicationService.UpsertAsync(request.FormType, request.Request, cancellationToken);
    }
}
