using React_Receiver.Models;

namespace React_Receiver.Infrastructure.FormSchemas;

public interface IFormSchemaRepository
{
    bool IsConfigured { get; }
    Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken);
    Task<ResourceEnvelope<FormSchemaResponse>?> GetAsync(string formType, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string formType, CancellationToken cancellationToken);
    Task<UpsertResult<FormSchemaResponse>> UpsertAsync(string formType, FormSchemaResponse request, string? expectedETag, CancellationToken cancellationToken);
}
