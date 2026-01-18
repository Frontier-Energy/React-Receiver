namespace React_Receiver.Services;

public sealed class QueueStorageOptions
{
    public string ConnectionString { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
}
