using System.Collections.Concurrent;
using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;

namespace React_Receiver.Services;

public interface ITranslationService
{
    Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken);
    Task<TranslationsResponse?> UpsertAsync(string language, TranslationsResponse request, CancellationToken cancellationToken);
    Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken);
}

public sealed class TranslationService : ITranslationService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly ConcurrentDictionary<string, TranslationsResponse> _translations;

    public TranslationService(
        TableServiceClient tableServiceClient,
        IBootstrapDataProvider bootstrapDataProvider,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
        _translations = new ConcurrentDictionary<string, TranslationsResponse>(
            bootstrapDataProvider
                .GetTranslations()
                .Select(item => new KeyValuePair<string, TranslationsResponse>(
                    item.Language,
                    item.Payload)),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken)
    {
        if (!HasTableStorageConfiguration())
        {
            return _translations.TryGetValue(language, out var defaultTranslations)
                ? defaultTranslations
                : null;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        try
        {
            var response = await tableClient.GetEntityAsync<TranslationEntity>(
                TranslationEntity.PartitionKeyValue,
                language,
                cancellationToken: cancellationToken);
            return response.Value.ToResponse();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<TranslationsResponse?> UpsertAsync(
        string language,
        TranslationsResponse request,
        CancellationToken cancellationToken)
    {
        if (!HasTableStorageConfiguration())
        {
            _translations[language] = request;
            return request;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await tableClient.UpsertEntityAsync(
            TranslationEntity.FromResponse(language, request),
            TableUpdateMode.Replace,
            cancellationToken);

        _translations[language] = request;
        return request;
    }

    public async Task ImportSeedDataAsync(bool overwriteExisting, CancellationToken cancellationToken)
    {
        if (!HasTableStorageConfiguration())
        {
            return;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        foreach (var translation in _translations)
        {
            if (!overwriteExisting && await TranslationExistsAsync(tableClient, translation.Key, cancellationToken))
            {
                continue;
            }

            await tableClient.UpsertEntityAsync(
                TranslationEntity.FromResponse(translation.Key, translation.Value),
                TableUpdateMode.Replace,
                cancellationToken);
        }
    }

    private bool HasTableStorageConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
               !string.IsNullOrWhiteSpace(_tableOptions.TranslationsTableName);
    }

    private static async Task<bool> TranslationExistsAsync(
        TableClient tableClient,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            await tableClient.GetEntityAsync<TranslationEntity>(
                TranslationEntity.PartitionKeyValue,
                language,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }
}
