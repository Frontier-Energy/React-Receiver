using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using React_Receiver.Services;
using Xunit;

namespace React_Receiver.Tests;

public sealed class StorageStartupValidationTests
{
    [Fact]
    public void BlobStorageOptionsValidator_ReturnsFailures_ForMissingRequiredValues()
    {
        var validator = new BlobStorageOptionsValidator();

        var result = validator.Validate(
            name: null,
            new BlobStorageOptions
            {
                ConnectionString = string.Empty,
                ContainerName = string.Empty
            });

        Assert.False(result.Succeeded);
        var failures = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(result.Failures);
        Assert.Contains("BlobStorage:ConnectionString is required.", failures);
        Assert.Contains("BlobStorage:ContainerName is required.", failures);
    }

    [Fact]
    public void TableStorageOptionsValidator_ReturnsFailures_ForMissingTableNames()
    {
        var validator = new TableStorageOptionsValidator();

        var result = validator.Validate(
            name: null,
            new TableStorageOptions
            {
                ConnectionString = string.Empty,
                TableName = string.Empty,
                InspectionFilesTableName = string.Empty,
                InspectionIngestOutboxTableName = string.Empty,
                TenantConfigTableName = string.Empty,
                MeTableName = string.Empty,
                FormSchemaCatalogTableName = string.Empty,
                FormSchemasTableName = string.Empty,
                TranslationsTableName = string.Empty
            });

        Assert.False(result.Succeeded);
        var failures = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(result.Failures);
        Assert.Contains("TableStorage:ConnectionString is required.", failures);
        Assert.Contains("TableStorage:InspectionIngestOutboxTableName is required.", failures);
        Assert.Contains("TableStorage:FormSchemasTableName is required.", failures);
        Assert.Contains("TableStorage:TranslationsTableName is required.", failures);
    }

    [Fact]
    public async Task StartupHealthCheckHostedService_Throws_WhenStartupChecksFail()
    {
        var healthCheckService = new StubHealthCheckService(new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["blob-storage"] = new(
                    status: HealthStatus.Unhealthy,
                    description: "Blob storage dependency check failed.",
                    duration: TimeSpan.Zero,
                    exception: null,
                    data: new Dictionary<string, object>())
            },
            TimeSpan.Zero));
        var hostedService = new StartupHealthCheckHostedService(
            healthCheckService,
            NullLogger<StartupHealthCheckHostedService>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => hostedService.StartAsync(CancellationToken.None));

        Assert.Contains("blob-storage", exception.Message);
    }

    [Fact]
    public async Task StorageConfigurationHealthCheck_ReturnsUnhealthy_WhenRequiredConfigMissing()
    {
        var healthCheck = new StorageConfigurationHealthCheck(
            Options.Create(new BlobStorageOptions()),
            Options.Create(new QueueStorageOptions()),
            Options.Create(new TableStorageOptions
            {
                ConnectionString = string.Empty,
                TableName = string.Empty
            }));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        var missing = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(result.Data["missing"]);
        Assert.Contains("BlobStorage:ConnectionString", missing);
        Assert.Contains("QueueStorage:QueueName", missing);
        Assert.Contains("TableStorage:TableName", missing);
    }

    [Theory]
    [InlineData("Infrastructure/Inspections/AzureInspectionRepository.cs")]
    [InlineData("Infrastructure/TenantConfig/AzureTableTenantConfigRepository.cs")]
    [InlineData("Infrastructure/Translations/AzureTableTranslationRepository.cs")]
    [InlineData("Infrastructure/FormSchemas/AzureFormSchemaRepository.cs")]
    [InlineData("Infrastructure/Users/AzureTableUserRepository.cs")]
    public void RepositorySources_DoNotProvisionStorageInfrastructure_OnRequestPath(string relativePath)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(repositoryRoot, relativePath));

        Assert.DoesNotContain("CreateIfNotExistsAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateIfNotExists(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UserRepository_FindByEmail_HandlesMissingUsersTable()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(repositoryRoot, "Infrastructure/Users/AzureTableUserRepository.cs"));

        Assert.Contains("catch (RequestFailedException ex) when (ex.Status == 404)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersStorageInfrastructureHostedService_BeforeStartupHealthChecks()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(repositoryRoot, "Program.cs"));

        var storageIndex = source.IndexOf("AddHostedService<StorageInfrastructureHostedService>()", StringComparison.Ordinal);
        var startupIndex = source.IndexOf("AddHostedService<StartupHealthCheckHostedService>()", StringComparison.Ordinal);

        Assert.True(storageIndex >= 0, "Storage infrastructure hosted service registration was not found.");
        Assert.True(startupIndex >= 0, "Startup health check hosted service registration was not found.");
        Assert.True(storageIndex < startupIndex, "Storage infrastructure must start before startup health checks.");
    }

    private sealed class StubHealthCheckService : HealthCheckService
    {
        private readonly HealthReport _report;

        public StubHealthCheckService(HealthReport report)
        {
            _report = report;
        }

        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_report);
        }
    }
}
