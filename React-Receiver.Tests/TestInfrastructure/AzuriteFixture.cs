using System.Diagnostics;
using System.Runtime.InteropServices;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Observability;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests.TestInfrastructure;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class AzuriteCollectionDefinition : ICollectionFixture<AzuriteFixture>
{
    public const string CollectionName = "AzuriteIntegration";
}

public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string DevelopmentStorageConnectionString = "UseDevelopmentStorage=true";
    private const string AzuriteExecutablePathEnvironmentVariable = "AZURITE_EXECUTABLE_PATH";
    private readonly string _repositoryRoot;
    private readonly string _azuriteExecutablePath;
    private readonly string? _azuriteArgumentsPrefix;
    private readonly string _dataDirectory;
    private Process? _azuriteProcess;
    private bool _ownsAzuriteProcess;

    public AzuriteFixture()
    {
        _repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        (_azuriteExecutablePath, _azuriteArgumentsPrefix) = ResolveAzuriteCommand(_repositoryRoot);
        _dataDirectory = Path.Combine(Path.GetTempPath(), "react-receiver-azurite", Guid.NewGuid().ToString("N"));
        BlobServiceClient = new BlobServiceClient(DevelopmentStorageConnectionString);
        QueueServiceClient = new QueueServiceClient(DevelopmentStorageConnectionString);
        TableServiceClient = new TableServiceClient(DevelopmentStorageConnectionString);
        StorageObserver = new StorageOperationObserver(NullLogger<StorageOperationObserver>.Instance);
    }

    public BlobServiceClient BlobServiceClient { get; }

    public QueueServiceClient QueueServiceClient { get; }

    public TableServiceClient TableServiceClient { get; }

    public IStorageOperationObserver StorageObserver { get; }

    public async Task InitializeAsync()
    {
        if (!await IsAzuriteReadyAsync())
        {
            StartAzuriteProcess();
            _ownsAzuriteProcess = true;
            await WaitForAzuriteAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_azuriteProcess is not null && !_azuriteProcess.HasExited)
        {
            _azuriteProcess.Kill(entireProcessTree: true);
            await _azuriteProcess.WaitForExitAsync();
            _azuriteProcess.Dispose();
            _azuriteProcess = null;
        }

        if (_ownsAzuriteProcess && Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    public async Task<StorageTestScope> CreateScopeAsync()
    {
        var scope = new StorageTestScope(this);
        await scope.InitializeAsync();
        return scope;
    }

    internal AzureInspectionRepository CreateRepository(StorageTestScope scope, InspectionIngestRetryOptions? retryOptions = null)
    {
        return CreateRepository(
            scope,
            new InspectionFileSecurityInspector(new SignatureInspectionFileMalwareScanner()),
            retryOptions);
    }

    internal AzureInspectionRepository CreateRepository(
        StorageTestScope scope,
        IInspectionFileSecurityInspector fileSecurityInspector,
        InspectionIngestRetryOptions? retryOptions = null)
    {
        return new AzureInspectionRepository(
            BlobServiceClient,
            QueueServiceClient,
            TableServiceClient,
            Options.Create(scope.BlobOptions),
            Options.Create(scope.QueueOptions),
            Options.Create(scope.TableOptions),
            Options.Create(retryOptions ?? new InspectionIngestRetryOptions { PoisonThreshold = 3 }),
            StorageObserver,
            fileSecurityInspector);
    }

    internal AzureInspectionOutboxStore CreateOutboxStore(StorageTestScope scope)
    {
        return new AzureInspectionOutboxStore(TableServiceClient, scope.TableOptions, StorageObserver);
    }

    private async Task<bool> IsAzuriteReadyAsync()
    {
        try
        {
            await BlobServiceClient.GetPropertiesAsync();
            await QueueServiceClient.GetPropertiesAsync();
            var tableName = CreateTableName("ping");
            await TableServiceClient.CreateTableIfNotExistsAsync(tableName);
            await TableServiceClient.DeleteTableAsync(tableName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void StartAzuriteProcess()
    {
        if (!CanStartExecutable(_azuriteExecutablePath))
        {
            throw new InvalidOperationException($"Azurite executable was not found at '{_azuriteExecutablePath}'.");
        }

        Directory.CreateDirectory(_dataDirectory);
        _azuriteProcess = Process.Start(new ProcessStartInfo
        {
            FileName = _azuriteExecutablePath,
            Arguments = BuildAzuriteArguments(_dataDirectory),
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start Azurite.");
    }

    private string BuildAzuriteArguments(string dataDirectory)
    {
        var azuriteArguments = $"--silent --location \"{dataDirectory}\"";
        return string.IsNullOrWhiteSpace(_azuriteArgumentsPrefix)
            ? azuriteArguments
            : $"{_azuriteArgumentsPrefix} {azuriteArguments}";
    }

    private async Task WaitForAzuriteAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await IsAzuriteReadyAsync())
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(250);
        }

        throw new InvalidOperationException("Azurite did not become ready within 15 seconds.", lastException);
    }

    internal static string CreateTableName(string prefix)
    {
        return $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 12, 63)];
    }

    internal static string CreateContainerName(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 33, 63)];
    }

    internal static string CreateQueueName(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 33, 63)];
    }

    private static (string ExecutablePath, string? ArgumentsPrefix) ResolveAzuriteCommand(string repositoryRoot)
    {
        var configuredPath = Environment.GetEnvironmentVariable(AzuriteExecutablePathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath, repositoryRoot);
            return (fullPath, null);
        }

        var npmBinDirectory = Path.Combine(repositoryRoot, "node_modules", ".bin");
        var platformShim = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(npmBinDirectory, "azurite.cmd")
            : Path.Combine(npmBinDirectory, "azurite");

        if (File.Exists(platformShim))
        {
            return (platformShim, null);
        }

        var fallbackShim = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(npmBinDirectory, "azurite")
            : Path.Combine(npmBinDirectory, "azurite.cmd");

        if (File.Exists(fallbackShim))
        {
            return (fallbackShim, null);
        }

        var packageEntryPoint = Path.Combine(repositoryRoot, "node_modules", "azurite", "dist", "src", "azurite.js");
        if (File.Exists(packageEntryPoint))
        {
            return ("node", $"\"{packageEntryPoint}\"");
        }

        return (platformShim, null);
    }

    private static bool CanStartExecutable(string executablePath)
    {
        return !Path.IsPathRooted(executablePath) || File.Exists(executablePath);
    }
}

public sealed class StorageTestScope : IAsyncDisposable
{
    private readonly AzuriteFixture _fixture;

    internal StorageTestScope(AzuriteFixture fixture)
    {
        _fixture = fixture;
        BlobOptions = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = AzuriteFixture.CreateContainerName("inspect")
        };
        QueueOptions = new QueueStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            QueueName = AzuriteFixture.CreateQueueName("inspection")
        };
        TableOptions = new TableStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            InspectionFilesTableName = AzuriteFixture.CreateTableName("inspectionfiles"),
            InspectionIngestOutboxTableName = AzuriteFixture.CreateTableName("inspectionoutbox")
        };
    }

    public BlobStorageOptions BlobOptions { get; }

    public QueueStorageOptions QueueOptions { get; }

    public TableStorageOptions TableOptions { get; }

    public BlobContainerClient PayloadContainer => _fixture.BlobServiceClient.GetBlobContainerClient(BlobOptions.ContainerName);

    public BlobContainerClient FilesContainer => _fixture.BlobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesContainerName);

    public BlobContainerClient QuarantineContainer => _fixture.BlobServiceClient.GetBlobContainerClient(StorageDependencyNames.FilesQuarantineContainerName);

    public QueueClient QueueClient => _fixture.QueueServiceClient.GetQueueClient(QueueOptions.QueueName);

    public TableClient OutboxTable => _fixture.TableServiceClient.GetTableClient(TableOptions.InspectionIngestOutboxTableName);

    public TableClient FilesTable => _fixture.TableServiceClient.GetTableClient(TableOptions.InspectionFilesTableName);

    internal async Task InitializeAsync()
    {
        await PayloadContainer.CreateIfNotExistsAsync();
        await FilesContainer.CreateIfNotExistsAsync();
        await QuarantineContainer.CreateIfNotExistsAsync();
        await QueueClient.CreateIfNotExistsAsync();
        await _fixture.TableServiceClient.CreateTableIfNotExistsAsync(TableOptions.InspectionFilesTableName);
        await _fixture.TableServiceClient.CreateTableIfNotExistsAsync(TableOptions.InspectionIngestOutboxTableName);
    }

    public async ValueTask DisposeAsync()
    {
        await DeleteIfExistsAsync(() => FilesTable.DeleteAsync());
        await DeleteIfExistsAsync(() => OutboxTable.DeleteAsync());
        await PayloadContainer.DeleteIfExistsAsync();
        await FilesContainer.DeleteIfExistsAsync();
        await QuarantineContainer.DeleteIfExistsAsync();
        await QueueClient.DeleteIfExistsAsync();
    }

    private static async Task DeleteIfExistsAsync(Func<Task<Response>> deleteAsync)
    {
        try
        {
            await deleteAsync();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
        }
    }
}
