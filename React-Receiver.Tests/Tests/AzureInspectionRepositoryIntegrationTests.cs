using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;
using React_Receiver.Services;
using React_Receiver.Tests.TestInfrastructure;
using Xunit;

namespace React_Receiver.Tests;

[Collection(AzuriteCollectionDefinition.CollectionName)]
public sealed class AzureInspectionRepositoryIntegrationTests
{
    private readonly AzuriteFixture _fixture;

    public AzureInspectionRepositoryIntegrationTests(AzuriteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PrepareAsync_StagesPayloadFiles_AndOutboxStateInAzurite()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(scope);
        var request = CreateRequest("session-stage");

        var response = await repository.PrepareAsync(request, CancellationToken.None);

        Assert.Equal(request.SessionId, response.SessionId);

        var payloadBlob = scope.PayloadContainer.GetBlobClient($"{request.SessionId}.json");
        var payload = await payloadBlob.DownloadContentAsync();
        using var payloadDocument = JsonDocument.Parse(payload.Value.Content.ToString());
        Assert.Equal(request.SessionId, payloadDocument.RootElement.GetProperty("SessionId").GetString());

        var quarantineBlob = scope.QuarantineContainer.GetBlobClient($"{request.SessionId}-evidence.txt");
        var quarantineProperties = await quarantineBlob.GetPropertiesAsync();
        Assert.Equal(InspectionFileBlobMetadata.PendingStatus, quarantineProperties.Value.Metadata[InspectionFileBlobMetadata.VerificationStatusKey]);
        Assert.Equal("text/plain", quarantineProperties.Value.ContentType);

        var outbox = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        Assert.True(outbox.Value.PayloadStaged);
        Assert.True(outbox.Value.FilesStaged);
        Assert.Equal("Quarantined", outbox.Value.Status);
        Assert.False(outbox.Value.Completed);
    }

    [Fact]
    public async Task ProcessPendingAsync_PromotesFiles_WritesMetadata_QueuesMessage_AndCompletesWorkflow()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(scope);
        var request = CreateRequest("session-process");

        await repository.PrepareAsync(request, CancellationToken.None);

        var result = await repository.ProcessPendingAsync(request.SessionId!, CancellationToken.None);

        Assert.True(result.Processed);
        Assert.Equal("Completed", result.Status);

        var finalBlob = scope.FilesContainer.GetBlobClient($"{request.SessionId}-evidence.txt");
        var finalProperties = await finalBlob.GetPropertiesAsync();
        Assert.Equal(InspectionFileBlobMetadata.VerifiedStatus, finalProperties.Value.Metadata[InspectionFileBlobMetadata.VerificationStatusKey]);
        Assert.False((await scope.QuarantineContainer.GetBlobClient($"{request.SessionId}-evidence.txt").ExistsAsync()).Value);

        var metadata = await scope.FilesTable.GetEntityAsync<InspectionFilesEntity>(
            InspectionFilesEntity.PartitionKeyValue,
            request.SessionId!);
        var files = JsonSerializer.Deserialize<InspectionFileReference[]>(metadata.Value.Files);
        var file = Assert.Single(files!);
        Assert.Equal("evidence.txt", file.FileName);
        Assert.Equal(request.SessionId, file.SessionId);

        var messages = await scope.QueueClient.ReceiveMessagesAsync(maxMessages: 1);
        var message = Assert.Single(messages.Value);
        Assert.Contains(request.SessionId!, message.MessageText, StringComparison.Ordinal);

        var outbox = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        Assert.True(outbox.Value.FilesVerified);
        Assert.True(outbox.Value.MetadataWritten);
        Assert.True(outbox.Value.QueueMessageSent);
        Assert.True(outbox.Value.Completed);
        Assert.False(outbox.Value.Processing);
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenQuarantineBlobIsMissing_SchedulesRetry()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(
            scope,
            new InspectionIngestRetryOptions { PoisonThreshold = 3 });
        var request = CreateRequest("session-retry");

        await repository.PrepareAsync(request, CancellationToken.None);
        await scope.QuarantineContainer.GetBlobClient($"{request.SessionId}-evidence.txt").DeleteIfExistsAsync();

        var before = DateTimeOffset.UtcNow;
        var result = await repository.ProcessPendingAsync(request.SessionId!, CancellationToken.None);

        Assert.False(result.Processed);
        Assert.Equal("PendingRetry", result.Status);
        Assert.Equal(1, result.RetryCount);
        Assert.False(result.TerminalFailure);
        Assert.Contains("was not found", result.LastError, StringComparison.Ordinal);

        var outbox = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        Assert.Equal(1, outbox.Value.RetryCount);
        Assert.Equal("PendingRetry", outbox.Value.Status);
        Assert.False(outbox.Value.Processing);
        Assert.NotNull(outbox.Value.NextAttemptAtUtc);
        Assert.True(outbox.Value.NextAttemptAtUtc > before);
        Assert.False(outbox.Value.TerminalFailure);

        Assert.False((await scope.FilesContainer.GetBlobClient($"{request.SessionId}-evidence.txt").ExistsAsync()).Value);
        await Assert.ThrowsAsync<RequestFailedException>(() => scope.FilesTable.GetEntityAsync<InspectionFilesEntity>(
            InspectionFilesEntity.PartitionKeyValue,
            request.SessionId!));
    }

    [Fact]
    public async Task TryAcquireLeaseAsync_OnlyOneConcurrentAttemptSucceeds()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(scope);
        var firstStore = _fixture.CreateOutboxStore(scope);
        var secondStore = _fixture.CreateOutboxStore(scope);
        var request = CreateRequest("session-lease");

        await repository.PrepareAsync(request, CancellationToken.None);

        var firstAttempt = firstStore.TryAcquireLeaseAsync(request.SessionId!, TimeSpan.FromMinutes(2), CancellationToken.None);
        var secondAttempt = secondStore.TryAcquireLeaseAsync(request.SessionId!, TimeSpan.FromMinutes(2), CancellationToken.None);
        var results = await Task.WhenAll(firstAttempt, secondAttempt);

        var leasedEntity = Assert.Single(results, entity => entity is not null);
        Assert.NotNull(leasedEntity);

        var persisted = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        Assert.True(persisted.Value.Processing);
        Assert.NotNull(persisted.Value.LockedUntilUtc);
        Assert.Equal("Processing", persisted.Value.Status);
    }

    private static ReceiveInspectionRequest CreateRequest(string sessionId)
    {
        return new ReceiveInspectionRequest(
            sessionId,
            "user-123",
            "Boiler room",
            new Dictionary<string, string> { ["floor"] = "B1" },
            [CreateTextFile("evidence.txt", $"inspection payload for {sessionId}")]);
    }

    private static IFormFile CreateTextFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "files", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
    }
}
