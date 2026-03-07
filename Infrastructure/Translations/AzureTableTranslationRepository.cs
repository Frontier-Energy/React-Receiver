using Azure;
using Azure.Data.Tables;
using React_Receiver.Application.Concurrency;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Translations;

public sealed class AzureTableTranslationRepository : ITranslationRepository
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;

    public AzureTableTranslationRepository(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions,
        IStorageOperationObserver storageObserver)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
        _storageObserver = storageObserver;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
        !string.IsNullOrWhiteSpace(_tableOptions.TranslationsTableName);

    public async Task<ResourceEnvelope<TranslationsResponse>?> GetAsync(string language, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        try
        {
            var response = await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TranslationsTableName,
                "GetTranslations",
                ct => tableClient.GetEntityIfExistsAsync<TranslationEntity>(
                    TranslationEntity.PartitionKeyValue,
                    language,
                    cancellationToken: ct),
                cancellationToken);
            return response.HasValue
                ? new ResourceEnvelope<TranslationsResponse>(
                    response.Value!.ToResponse(),
                    response.Value.ETag.ToString())
                : null;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string language, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        try
        {
            var response = await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TranslationsTableName,
                "TranslationsExists",
                ct => tableClient.GetEntityIfExistsAsync<TranslationEntity>(
                    TranslationEntity.PartitionKeyValue,
                    language,
                    cancellationToken: ct),
                cancellationToken);
            return response.HasValue;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<UpsertResult<TranslationsResponse>> UpsertAsync(
        string language,
        TranslationsResponse request,
        string? expectedETag,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        var existing = await GetEntityAsync(tableClient, language, cancellationToken);
        OptimisticConcurrency.EnsureSatisfied(expectedETag, existing?.ETag.ToString(), $"translations '{language}'");

        var entity = TranslationEntity.FromResponse(language, request);
        if (existing is null)
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TranslationsTableName,
                "AddTranslations",
                ct => tableClient.AddEntityAsync(entity, ct),
                cancellationToken);
        }
        else
        {
            await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TranslationsTableName,
                "UpdateTranslations",
                ct => tableClient.UpdateEntityAsync(entity, existing.ETag, TableUpdateMode.Replace, ct),
                cancellationToken);
        }

        var saved = await GetEntityAsync(tableClient, language, cancellationToken)
            ?? throw new InvalidOperationException($"Translations '{language}' could not be reloaded after save.");
        return new UpsertResult<TranslationsResponse>(request, existing is null, ETag: saved.ETag.ToString());
    }

    private async Task<TranslationEntity?> GetEntityAsync(
        TableClient tableClient,
        string language,
        CancellationToken cancellationToken)
    {
        var response = await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.TranslationsTableName,
            "GetTranslationsForWrite",
            ct => tableClient.GetEntityIfExistsAsync<TranslationEntity>(
                TranslationEntity.PartitionKeyValue,
                language,
                cancellationToken: ct),
            cancellationToken);

        return response.HasValue ? response.Value : null;
    }
}
