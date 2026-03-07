namespace React_Receiver.Services;

public sealed class TableStorageOptions
{
    public string? ConnectionString { get; set; }
    public string TableName { get; set; } = "Users";
    public string InspectionFilesTableName { get; set; } = "InspectionFiles";
    public string InspectionIngestOutboxTableName { get; set; } = "InspectionIngestOutbox";
    public string TenantConfigTableName { get; set; } = "TenantConfigs";
    public string MeTableName { get; set; } = "MeProfiles";
    public string FormSchemaCatalogTableName { get; set; } = "FormSchemaCatalog";
    public string FormSchemasTableName { get; set; } = "FormSchemas";
    public string TranslationsTableName { get; set; } = "Translations";
}
