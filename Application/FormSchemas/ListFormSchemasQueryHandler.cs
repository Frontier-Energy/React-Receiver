using MediatR;
using React_Receiver.Models;

namespace React_Receiver.Application.FormSchemas;

public sealed class ListFormSchemasQueryHandler : IRequestHandler<ListFormSchemasQuery, FormSchemaCatalogResponse>
{
    private readonly IFormSchemaApplicationService _formSchemaApplicationService;

    public ListFormSchemasQueryHandler(IFormSchemaApplicationService formSchemaApplicationService)
    {
        _formSchemaApplicationService = formSchemaApplicationService;
    }

    public Task<FormSchemaCatalogResponse> Handle(ListFormSchemasQuery request, CancellationToken cancellationToken)
    {
        return _formSchemaApplicationService.ListAsync(cancellationToken);
    }
}
