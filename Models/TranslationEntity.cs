using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class TranslationEntity : ITableEntity
{
    public const string PartitionKeyValue = "Translations";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string LanguageName { get; set; } = string.Empty;
    public string AppJson { get; set; } = "{}";

    public TranslationsResponse ToResponse()
    {
        var app = JsonSerializer.Deserialize<TranslationAppResponse>(AppJson) ??
                  new TranslationAppResponse(string.Empty, string.Empty, string.Empty);
        return new TranslationsResponse(LanguageName: LanguageName, App: app);
    }

    public static TranslationEntity FromResponse(string language, TranslationsResponse response)
    {
        return new TranslationEntity
        {
            PartitionKey = PartitionKeyValue,
            RowKey = language,
            LanguageName = response.LanguageName,
            AppJson = JsonSerializer.Serialize(response.App)
        };
    }
}
