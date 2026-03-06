using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;

namespace React_Receiver.Services;

public interface IUserQueryService
{
    Task<GetUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken);
    Task<MeResponse> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public sealed class UserQueryService : IUserQueryService
{
    private static readonly MeResponse DefaultCurrentUser = new(
        UserId: "a1b2c3",
        Roles: ["admin"],
        Permissions: ["tenant.select", "customization.admin"]);

    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public UserQueryService(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
    }

    public async Task<GetUserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.TableName))
        {
            return null;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<UserEntity>(
                UserEntity.PartitionKeyValue,
                userId,
                cancellationToken: cancellationToken);

            var entity = response.Value;
            return new GetUserResponse(
                new UserModel(
                    UserId: entity.UserId,
                    Email: entity.Email,
                    FirstName: entity.FirstName,
                    LastName: entity.LastName));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<MeResponse> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.MeTableName))
        {
            return DefaultCurrentUser;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.MeTableName);
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        try
        {
            var response = await tableClient.GetEntityAsync<MeEntity>(
                MeEntity.PartitionKeyValue,
                MeEntity.CurrentUserRowKey,
                cancellationToken: cancellationToken);
            return response.Value.ToResponse();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            await tableClient.UpsertEntityAsync(
                MeEntity.FromResponse(DefaultCurrentUser),
                TableUpdateMode.Replace,
                cancellationToken);
            return DefaultCurrentUser;
        }
    }
}
