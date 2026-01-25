using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Handlers;

public interface IRegisterRequestHandler
{
    Task<string> HandleRegisterAsync(RegisterRequestModel request, string userId, CancellationToken cancellationToken);
}

public sealed class RegisterRequestHandler : IRegisterRequestHandler
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public RegisterRequestHandler(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
    }

    public async Task<string> HandleRegisterAsync(
        RegisterRequestModel request,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_tableOptions.ConnectionString))
        {
            var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);
            await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            UserEntity? existing = null;
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var filter = TableClient.CreateQueryFilter<UserEntity>(entity =>
                    entity.PartitionKey == UserEntity.PartitionKeyValue &&
                    entity.Email == request.Email);
                await foreach (var entity in tableClient.QueryAsync<UserEntity>(
                    filter: filter,
                    cancellationToken: cancellationToken))
                {
                    existing = entity;
                    break;
                }
            }
            else
            {
                try
                {
                    var response = await tableClient.GetEntityAsync<UserEntity>(
                        UserEntity.PartitionKeyValue,
                        userId,
                        cancellationToken: cancellationToken);
                    existing = response.Value;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    existing = null;
                }
            }

            if (existing is null)
            {
                var entity = new UserEntity
                {
                    PartitionKey = UserEntity.PartitionKeyValue,
                    RowKey = userId,
                    UserId = userId,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName
                };

                await tableClient.AddEntityAsync(entity, cancellationToken);
            }
            else
            {
                userId = existing.UserId;
            }
        }

        return userId;
    }
}
