using System.Collections.Concurrent;
using React_Receiver.Infrastructure.FormSchemas;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Application.FormSchemas;

public sealed class FormSchemaApplicationService : IFormSchemaApplicationService
{
    private sealed record SeedEntry(string Version, string Etag, FormSchemaResponse Schema)
    {
        public FormSchemaCatalogItemResponse ToCatalogItem(string formType) => new(formType, Version, Etag);
    }

    private readonly IFormSchemaRepository _repository;
    private readonly ConcurrentDictionary<string, SeedEntry> _catalog;

    public FormSchemaApplicationService(
        IFormSchemaRepository repository,
        IBootstrapDataProvider bootstrapDataProvider)
    {
        _repository = repository;
        _catalog = new ConcurrentDictionary<string, SeedEntry>(
            bootstrapDataProvider
                .GetFormSchemas()
                .Select(item => new KeyValuePair<string, SeedEntry>(
                    item.FormType,
                    new SeedEntry(item.Version, item.Etag, item.Schema))),
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<FormSchemaCatalogResponse> ListAsync(CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return Task.FromResult(new FormSchemaCatalogResponse(
                _catalog.Select(item => item.Value.ToCatalogItem(item.Key)).ToArray()));
        }

        return _repository.ListAsync(cancellationToken);
    }

    public Task<FormSchemaResponse?> GetAsync(string formType, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return Task.FromResult(
                _catalog.TryGetValue(formType, out var entry)
                    ? entry.Schema
                    : null);
        }

        return _repository.GetAsync(formType, cancellationToken);
    }

    public async Task<UpsertResult<FormSchemaResponse>> UpsertAsync(
        string formType,
        FormSchemaResponse request,
        CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            var now = DateTime.UtcNow;
            var created = !_catalog.ContainsKey(formType);
            _catalog[formType] = new SeedEntry(
                now.ToString("yyyy-MM-dd"),
                $"\"{formType}-{now:yyyyMMddHHmmssfff}\"",
                request);

            var entry = _catalog[formType];
            return new UpsertResult<FormSchemaResponse>(request, created, entry.Version, entry.Etag);
        }

        return await _repository.UpsertAsync(formType, request, cancellationToken);
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!_repository.IsConfigured)
        {
            return;
        }

        foreach (var seed in _catalog)
        {
            if (!overwriteExisting && await _repository.ExistsAsync(seed.Key, cancellationToken))
            {
                continue;
            }

            await _repository.UpsertAsync(seed.Key, seed.Value.Schema, cancellationToken);
        }
    }
}
