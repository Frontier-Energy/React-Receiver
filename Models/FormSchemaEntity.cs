using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class FormSchemaEntity : ITableEntity
{
    public const string PartitionKeyValue = "FormSchemas";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string FormName { get; set; } = string.Empty;
    public string SectionsJson { get; set; } = "[]";

    public FormSchemaResponse ToResponse()
    {
        var sections = JsonSerializer.Deserialize<FormSectionResponse[]>(SectionsJson) ?? Array.Empty<FormSectionResponse>();
        return new FormSchemaResponse(FormName: FormName, Sections: sections);
    }

    public static FormSchemaEntity FromResponse(string formType, FormSchemaResponse response)
    {
        return new FormSchemaEntity
        {
            PartitionKey = PartitionKeyValue,
            RowKey = formType,
            FormName = response.FormName,
            SectionsJson = JsonSerializer.Serialize(response.Sections)
        };
    }
}
