using Azure;
using Azure.Data.Tables;
using React_Receiver.Models;
using React_Receiver.Services;

namespace React_Receiver.Handlers;

public interface ILoginRequestHandler
{
    LoginRequestResponse HandleLogin(LoginRequestCommand request);
}

public sealed class LoginRequestHandler : ILoginRequestHandler
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableStorageOptions _tableOptions;

    public LoginRequestHandler(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<TableStorageOptions> tableOptions)
    {
        _tableServiceClient = tableServiceClient;
        _tableOptions = tableOptions.Value;
    }

    public LoginRequestResponse HandleLogin(LoginRequestCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(_tableOptions.ConnectionString))
        {
            return new LoginRequestResponse(UserId: string.Empty);
        }

        var tableClient = _tableServiceClient.GetTableClient(_tableOptions.TableName);
        var filter = TableClient.CreateQueryFilter<UserEntity>(entity =>
            entity.PartitionKey == UserEntity.PartitionKeyValue &&
            entity.Email == request.Email);

        try
        {
            foreach (var entity in tableClient.Query<UserEntity>(filter: filter))
            {
                return new LoginRequestResponse(UserId: entity.UserId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new LoginRequestResponse(UserId: string.Empty);
        }

        return new LoginRequestResponse(UserId: string.Empty);
    }
}
