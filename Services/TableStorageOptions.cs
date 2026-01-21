namespace React_Receiver.Services;

public sealed class TableStorageOptions
{
    public string? ConnectionString { get; set; }
    public string TableName { get; set; } = "Users";
    public string InspectionFilesTableName { get; set; } = "InspectionFiles";
}
