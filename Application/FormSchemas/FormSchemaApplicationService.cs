using React_Receiver.Infrastructure.FormSchemas;
using React_Receiver.Models;

namespace React_Receiver.Application.FormSchemas;

public sealed class FormSchemaApplicationService : IFormSchemaApplicationService
{
    private readonly IFormSchemaRepository _repository;
    private readonly IFormSchemaSeedStore _seedStore;

    public FormSchemaApplicationService(
        IFormSchemaRepository repository,
        IFormSchemaSeedStore seedStore)
    {
        _repository = repository;
        _seedStore = seedStore;
    }

    public Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return Task.FromResult(new FormSchemaCatalogResponse(_seedStore.ListCatalogItems().ToArray()));
        }

        return _repository.ListAsync(cancellationToken);
    }

    public Task<ResourceEnvelope<FormSchemaResponse>?> GetAsync(string formType, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return Task.FromResult(_seedStore.Get(formType));
        }

        return _repository.GetAsync(formType, cancellationToken);
    }

    public async Task<UpsertResult<FormSchemaResponse>> UpsertAsync(
        string formType,
        FormSchemaResponse request,
        string? expectedETag,
        CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return _seedStore.Upsert(formType, request, expectedETag);
        }

        return await _repository.UpsertAsync(formType, request, expectedETag, cancellationToken);
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return;
        }

        foreach (var seed in _seedStore.GetAll())
        {
            if (!overwriteExisting && await _repository.ExistsAsync(seed.Key, cancellationToken))
            {
                continue;
            }

            await _repository.UpsertAsync(seed.Key, seed.Value, null, cancellationToken);
        }
    }
}
