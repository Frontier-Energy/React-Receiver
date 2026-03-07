using Azure;
using Azure.Data.Tables;
using React_Receiver.Domain.Users;
using React_Receiver.Models;
using React_Receiver.Observability;
using React_Receiver.Services;

namespace React_Receiver.Infrastructure.Users;

public sealed class AzureTableUserRepository : IUserRepository
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;
    private readonly IStorageOperationObserver _storageObserver;

    public AzureTableUserRepository(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions,
        IStorageOperationObserver storageObserver)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
        _storageObserver = storageObserver;
    }

    public async Task<UserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.TableName))
        {
            return null;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);

        try
        {
            var response = await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TableName,
                "GetUserById",
                ct => tableClient.GetEntityAsync<UserEntity>(
                    UserEntity.PartitionKeyValue,
                    userId,
                    cancellationToken: ct),
                cancellationToken);
            var entity = response.Value;
            return new UserProfile(entity.UserId, entity.Email, entity.FirstName, entity.LastName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<UserProfile?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_tableOptions.TableName))
        {
            return null;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);
        var filter = TableClient.CreateQueryFilter<UserEntity>(entity =>
            entity.PartitionKey == UserEntity.PartitionKeyValue &&
            entity.Email == email);

        try
        {
            return await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.TableName,
                "FindUserByEmail",
                async ct =>
                {
                    await foreach (var entity in tableClient.QueryAsync<UserEntity>(
                                       filter: filter,
                                       cancellationToken: ct))
                    {
                        return new UserProfile(entity.UserId, entity.Email, entity.FirstName, entity.LastName);
                    }

                    return null;
                },
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(UserProfile user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString))
        {
            return;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);
        await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.TableName,
            "AddUser",
            ct => tableClient.AddEntityAsync(
                new UserEntity
                {
                    PartitionKey = UserEntity.PartitionKeyValue,
                    RowKey = user.UserId,
                    UserId = user.UserId,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                },
                ct),
            cancellationToken);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.MeTableName))
        {
            return null;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.MeTableName);
        try
        {
            var response = await _storageObserver.ExecuteAsync(
                "table",
                _tableOptions.MeTableName,
                "GetCurrentUser",
                ct => tableClient.GetEntityAsync<MeEntity>(
                    MeEntity.PartitionKeyValue,
                    MeEntity.CurrentUserRowKey,
                    cancellationToken: ct),
                cancellationToken);
            var me = response.Value.ToResponse();
            return new CurrentUser(me.UserId, me.Roles, me.Permissions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveCurrentUserAsync(CurrentUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_tableOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_tableOptions.MeTableName))
        {
            return;
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.MeTableName);
        await _storageObserver.ExecuteAsync(
            "table",
            _tableOptions.MeTableName,
            "SaveCurrentUser",
            ct => tableClient.UpsertEntityAsync(
                MeEntity.FromResponse(new MeResponse(user.UserId, user.Roles, user.Permissions)),
                TableUpdateMode.Replace,
                ct),
            cancellationToken);
    }
}
