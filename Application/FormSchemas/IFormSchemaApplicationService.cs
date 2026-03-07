using React_Receiver.Models;

namespace React_Receiver.Application.FormSchemas;

public interface IFormSchemaApplicationService
{
    Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken);
    Task<ResourceEnvelope<FormSchemaResponse>?> GetAsync(string formType, CancellationToken cancellationToken);
    Task<UpsertResult<FormSchemaResponse>> UpsertAsync(string formType, FormSchemaResponse request, string? expectedETag, CancellationToken cancellationToken);
    Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken);
}
