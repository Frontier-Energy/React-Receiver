using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace React_Receiver.Models;

public sealed class MeEntity : ITableEntity
{
    public const string PartitionKeyValue = "Me";
    public const string CurrentUserRowKey = "current";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = CurrentUserRowKey;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string UserId { get; set; } = string.Empty;
    public string RolesJson { get; set; } = "[]";
    public string PermissionsJson { get; set; } = "[]";

    public MeResponse ToResponse()
    {
        var roles = JsonSerializer.Deserialize<string[]>(RolesJson) ?? Array.Empty<string>();
        var permissions = JsonSerializer.Deserialize<string[]>(PermissionsJson) ?? Array.Empty<string>();
        return new MeResponse(UserId: UserId, Roles: roles, Permissions: permissions);
    }

    public static MeEntity FromResponse(MeResponse response)
    {
        return new MeEntity
        {
            PartitionKey = PartitionKeyValue,
            RowKey = CurrentUserRowKey,
            UserId = response.UserId,
            RolesJson = JsonSerializer.Serialize(response.Roles),
            PermissionsJson = JsonSerializer.Serialize(response.Permissions)
        };
    }
}
