using Microsoft.Extensions.Options;

namespace React_Receiver.Services;

public sealed class BlobStorageOptionsValidator : IValidateOptions<BlobStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, BlobStorageOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("BlobStorage:ConnectionString is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ContainerName))
        {
            failures.Add("BlobStorage:ContainerName is required.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

public sealed class QueueStorageOptionsValidator : IValidateOptions<QueueStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, QueueStorageOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("QueueStorage:ConnectionString is required.");
        }

        if (string.IsNullOrWhiteSpace(options.QueueName))
        {
            failures.Add("QueueStorage:QueueName is required.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

public sealed class TableStorageOptionsValidator : IValidateOptions<TableStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, TableStorageOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            failures.Add("TableStorage:ConnectionString is required.");
        }

        AddRequiredFailure(failures, options.TableName, "TableStorage:TableName");
        AddRequiredFailure(failures, options.InspectionFilesTableName, "TableStorage:InspectionFilesTableName");
        AddRequiredFailure(failures, options.TenantConfigTableName, "TableStorage:TenantConfigTableName");
        AddRequiredFailure(failures, options.MeTableName, "TableStorage:MeTableName");
        AddRequiredFailure(failures, options.FormSchemaCatalogTableName, "TableStorage:FormSchemaCatalogTableName");
        AddRequiredFailure(failures, options.FormSchemasTableName, "TableStorage:FormSchemasTableName");
        AddRequiredFailure(failures, options.TranslationsTableName, "TableStorage:TranslationsTableName");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void AddRequiredFailure(List<string> failures, string? value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{key} is required.");
        }
    }
}
