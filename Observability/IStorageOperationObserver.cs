using System.Diagnostics;
using System.Diagnostics.Metrics;
using Azure;

namespace React_Receiver.Observability;

public interface IStorageOperationObserver
{
    Task ExecuteAsync(
        string dependencyType,
        string dependencyName,
        string operation,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);

    Task<T> ExecuteAsync<T>(
        string dependencyType,
        string dependencyName,
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);
}

public sealed class StorageOperationObserver : IStorageOperationObserver
{
    private readonly ILogger<StorageOperationObserver> _logger;

    public StorageOperationObserver(ILogger<StorageOperationObserver> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(
        string dependencyType,
        string dependencyName,
        string operation,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync<object?>(
            dependencyType,
            dependencyName,
            operation,
            async ct =>
            {
                await action(ct);
                return null;
            },
            cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        string dependencyType,
        string dependencyName,
        string operation,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = "success";

        try
        {
            return await action(cancellationToken);
        }
        catch (RequestFailedException ex)
        {
            outcome = "failure";
            RecordFailure(dependencyType, dependencyName, operation, ex.Status, ex.ErrorCode);
            _logger.LogError(
                ex,
                "Storage operation failed for {DependencyType}/{DependencyName} during {Operation} with status {Status} and error code {ErrorCode}",
                dependencyType,
                dependencyName,
                operation,
                ex.Status,
                ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            outcome = "failure";
            RecordFailure(dependencyType, dependencyName, operation, null, null);
            _logger.LogError(
                ex,
                "Storage operation failed for {DependencyType}/{DependencyName} during {Operation}",
                dependencyType,
                dependencyName,
                operation);
            throw;
        }
        finally
        {
            var tags = new TagList
            {
                { "dependency.type", dependencyType },
                { "dependency.name", dependencyName },
                { "storage.operation", operation },
                { "outcome", outcome }
            };

            ReceiverTelemetry.StorageOperationDurationMs.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                tags);
        }
    }

    private static void RecordFailure(
        string dependencyType,
        string dependencyName,
        string operation,
        int? status,
        string? errorCode)
    {
        var tags = new TagList
        {
            { "dependency.type", dependencyType },
            { "dependency.name", dependencyName },
            { "storage.operation", operation }
        };

        if (status is not null)
        {
            tags.Add("azure.status", status.Value);
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            tags.Add("azure.error_code", errorCode);
        }

        ReceiverTelemetry.StorageOperationFailures.Add(1, tags);
    }
}
