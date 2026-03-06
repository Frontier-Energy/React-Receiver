using System.Collections.Concurrent;
using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;

namespace React_Receiver.Services;

public interface ITranslationService
{
    Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken);
    Task<TranslationsResponse?> UpsertAsync(string language, TranslationsResponse request, CancellationToken cancellationToken);
}

public sealed class TranslationService : ITranslationService
{
    private static readonly ConcurrentDictionary<string, TranslationsResponse> Translations =
        new(
            new Dictionary<string, TranslationsResponse>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = new(
                    LanguageName: "English",
                    App: new TranslationAppResponse(
                        Title: "Data Intake Tool",
                        PoweredBy: "Powered By",
                        Brand: "QControl")),
                ["es"] = new(
                    LanguageName: "Espanol",
                    App: new TranslationAppResponse(
                        Title: "Herramienta de Captura de Datos",
                        PoweredBy: "Desarrollado por",
                        Brand: "QControl"))
            });

    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public TranslationService(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
    }

    public async Task<TranslationsResponse?> GetAsync(string language, CancellationToken cancellationToken)
    {
        if (!Translations.TryGetValue(language, out var defaultTranslations))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.TranslationsTableName))
        {
            return defaultTranslations;
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
            await tableClient.UpsertEntityAsync(
                TranslationEntity.FromResponse(language, defaultTranslations),
                TableUpdateMode.Replace,
                cancellationToken);
            return defaultTranslations;
        }
    }

    public async Task<TranslationsResponse?> UpsertAsync(
        string language,
        TranslationsResponse request,
        CancellationToken cancellationToken)
    {
        if (!Translations.ContainsKey(language))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.TranslationsTableName))
        {
            Translations[language] = request;
            return request;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TranslationsTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await tableClient.UpsertEntityAsync(
            TranslationEntity.FromResponse(language, request),
            TableUpdateMode.Replace,
            cancellationToken);

        Translations[language] = request;
        return request;
    }
}
