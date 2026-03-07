using Azure;
using Azure.Data.Tables;
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

    public async Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken)
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
                ? response.Value!.ToResponse()
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
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        var created = !await ExistsAsync(language, cancellationToken);
        await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.TranslationsTableName,
            "UpsertTranslations",
            ct => tableClient.UpsertEntityAsync(
                TranslationEntity.FromResponse(language, request),
                TableUpdateMode.Replace,
                ct),
            cancellationToken);
        return new UpsertResult<TranslationsResponse>(request, created);
    }
}
