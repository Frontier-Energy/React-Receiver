using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.FormSchemas;

public sealed class GetFormSchemaQueryHandler : IRequestHandler<GetFormSchemaQuery, FormSchemaResponse?>
{
    private readonly IFormSchemaApplicationService _formSchemaApplicationService;

    public GetFormSchemaQueryHandler(IFormSchemaApplicationService formSchemaApplicationService)
    {
        _formSchemaApplicationService = formSchemaApplicationService;
    }

    public Task<FormSchemaResponse?> Handle(GetFormSchemaQuery request, CancellationToken cancellationToken)
    {
        return _formSchemaApplicationService.GetAsync(request.FormType, cancellationToken);
    }
}
