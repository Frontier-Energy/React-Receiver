using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Translations;

public sealed class AzureTableTranslationRepository : ITranslationRepository
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public AzureTableTranslationRepository(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) &&
        !string.IsNullOrWhiteSpace(_tableOptions.TranslationsTableName);

    public async Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken)
    {
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

    public async Task<bool> ExistsAsync(string language, CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
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

    public async Task<UpsertResult<TranslationsResponse>> UpsertAsync(
        string language,
        TranslationsResponse request,
        CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var created = !await ExistsAsync(language, cancellationToken);
        await tableClient.UpsertEntityAsync(
            TranslationEntity.FromResponse(language, request),
            TableUpdateMode.Replace,
            cancellationToken);
        return new UpsertResult<TranslationsResponse>(request, created);
    }
}
