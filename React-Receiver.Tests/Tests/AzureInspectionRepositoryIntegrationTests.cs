using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Mediation.Exceptions;
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
    public async Task ProcessPendingAsync_ReachesPoisonThreshold_AndReplayRequiresForce()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(
            scope,
            new InspectionIngestRetryOptions { PoisonThreshold = 2 });
        var request = CreateRequest("session-poison");

        await repository.PrepareAsync(request, CancellationToken.None);
        await scope.QuarantineContainer.GetBlobClient($"{request.SessionId}-evidence.txt").DeleteIfExistsAsync();

        var firstAttempt = await repository.ProcessPendingAsync(request.SessionId!, CancellationToken.None);
        var pendingRetry = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        pendingRetry.Value.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await scope.OutboxTable.UpdateEntityAsync(
            pendingRetry.Value,
            pendingRetry.Value.ETag,
            TableUpdateMode.Replace);
        var secondAttempt = await repository.ProcessPendingAsync(request.SessionId!, CancellationToken.None);

        Assert.False(firstAttempt.Processed);
        Assert.Equal("PendingRetry", firstAttempt.Status);
        Assert.False(secondAttempt.Processed);
        Assert.Equal(InspectionIngestStateMachine.PoisonedStatus, secondAttempt.Status);
        Assert.True(secondAttempt.TerminalFailure);
        Assert.Equal(2, secondAttempt.RetryCount);

        var blockedReplay = await repository.ReplayOutboxSessionAsync(request.SessionId!, force: false, CancellationToken.None);
        Assert.False(blockedReplay.Accepted);
        Assert.Contains("require force", blockedReplay.Message, StringComparison.OrdinalIgnoreCase);

        var forcedReplay = await repository.ReplayOutboxSessionAsync(request.SessionId!, force: true, CancellationToken.None);
        Assert.True(forcedReplay.Accepted);
        Assert.NotNull(forcedReplay.Session);
        Assert.Equal(InspectionIngestStateMachine.ReplayQueuedStatus, forcedReplay.Session!.Status);
        Assert.False(forcedReplay.Session.TerminalFailure);
        Assert.False(forcedReplay.Session.Processing);
        Assert.Null(forcedReplay.Session.LockedUntilUtc);
        Assert.Null(forcedReplay.Session.PoisonedAtUtc);
        Assert.NotNull(forcedReplay.Session.NextAttemptAtUtc);

        var persisted = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        Assert.Equal(InspectionIngestStateMachine.ReplayQueuedStatus, persisted.Value.Status);
        Assert.False(persisted.Value.TerminalFailure);
        Assert.False(persisted.Value.Processing);
        Assert.Null(persisted.Value.LockedUntilUtc);
        Assert.Null(persisted.Value.PoisonedAtUtc);
    }

    [Fact]
    public async Task ReplayOutboxSessionAsync_RejectsActiveLeaseWithoutForce()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(scope);
        var request = CreateRequest("session-replay-lease");

        await repository.PrepareAsync(request, CancellationToken.None);
        var lease = await _fixture.CreateOutboxStore(scope)
            .TryAcquireLeaseAsync(request.SessionId!, TimeSpan.FromMinutes(2), CancellationToken.None);
        Assert.NotNull(lease);

        var replay = await repository.ReplayOutboxSessionAsync(request.SessionId!, force: false, CancellationToken.None);

        Assert.False(replay.Accepted);
        Assert.Contains("currently leased", replay.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(replay.Session);
        Assert.True(replay.Session!.Processing);
        Assert.NotNull(replay.Session.LockedUntilUtc);
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

    [Fact]
    public async Task GetAsync_AfterCompletion_ReturnsPayloadAndPromotedFileMetadata()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(scope);
        var request = CreateRequest("session-get");

        await repository.PrepareAsync(request, CancellationToken.None);
        await repository.ProcessPendingAsync(request.SessionId!, CancellationToken.None);

        var inspection = await repository.GetAsync(request.SessionId!, CancellationToken.None);

        Assert.NotNull(inspection);
        Assert.Equal(request.SessionId, inspection!.SessionId);
        Assert.Equal(request.UserId, inspection.UserId);
        Assert.Equal(request.Name, inspection.Name);
        Assert.Equal("B1", inspection.QueryParams["floor"]);
        var file = Assert.Single(inspection.Files);
        Assert.Equal("evidence.txt", file.FileName);
        Assert.Equal("text/plain", file.FileType);
    }

    [Fact]
    public async Task GetFileAsync_AfterCompletion_ReturnsPromotedBlobStream()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(scope);
        var request = CreateRequest("session-file");

        await repository.PrepareAsync(request, CancellationToken.None);
        await repository.ProcessPendingAsync(request.SessionId!, CancellationToken.None);

        var file = await repository.GetFileAsync(request.SessionId!, "evidence.txt", CancellationToken.None);

        Assert.NotNull(file);
        Assert.Equal("evidence.txt", file!.FileName);
        Assert.Equal("text/plain", file.ContentType);
        using var reader = new StreamReader(file.Content, leaveOpen: false);
        var content = await reader.ReadToEndAsync();
        Assert.Equal($"inspection payload for {request.SessionId}", content);
    }

    [Fact]
    public async Task PrepareAsync_WhenFileIsRejectedBySecurityInspection_MarksRejected_AndLeavesQuarantineBlob()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(scope);
        var request = new ReceiveInspectionRequest(
            "session-rejected",
            "user-123",
            "Boiler room",
            new Dictionary<string, string> { ["floor"] = "B1" },
            [CreateTextFile(
                "evidence.txt",
                "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*")]);

        var exception = await Assert.ThrowsAsync<InspectionFileSecurityException>(
            () => repository.PrepareAsync(request, CancellationToken.None));

        Assert.Contains("malware scanning", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False((await scope.PayloadContainer.GetBlobClient($"{request.SessionId}.json").ExistsAsync()).Value);
        Assert.True((await scope.QuarantineContainer.GetBlobClient($"{request.SessionId}-evidence.txt").ExistsAsync()).Value);
        Assert.False((await scope.FilesContainer.GetBlobClient($"{request.SessionId}-evidence.txt").ExistsAsync()).Value);

        var outbox = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        Assert.Equal(InspectionIngestStateMachine.RejectedStatus, outbox.Value.Status);
        Assert.True(outbox.Value.TerminalFailure);
        Assert.False(outbox.Value.Completed);
        Assert.False(outbox.Value.Processing);
    }

    [Fact]
    public async Task PrepareAsync_WhenGenericFailureOccursDuringStaging_CompensatesPayloadAndQuarantineArtifacts()
    {
        await using var scope = await _fixture.CreateScopeAsync();
        var repository = _fixture.CreateRepository(
            scope,
            new SelectiveThrowingInspector("second.txt"));
        var request = new ReceiveInspectionRequest(
            "session-compensated",
            "user-123",
            "Boiler room",
            new Dictionary<string, string> { ["floor"] = "B1" },
            [
                CreateTextFile("first.txt", "first file"),
                CreateTextFile("second.txt", "second file")
            ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.PrepareAsync(request, CancellationToken.None));

        Assert.Equal("simulated inspection failure", exception.Message);
        Assert.False((await scope.PayloadContainer.GetBlobClient($"{request.SessionId}.json").ExistsAsync()).Value);
        Assert.False((await scope.QuarantineContainer.GetBlobClient($"{request.SessionId}-first.txt").ExistsAsync()).Value);
        Assert.False((await scope.QuarantineContainer.GetBlobClient($"{request.SessionId}-second.txt").ExistsAsync()).Value);
        Assert.False((await scope.FilesContainer.GetBlobClient($"{request.SessionId}-first.txt").ExistsAsync()).Value);

        var outbox = await scope.OutboxTable.GetEntityAsync<InspectionIngestOutboxEntity>(
            InspectionIngestOutboxEntity.PartitionKeyValue,
            request.SessionId!);
        Assert.Equal(InspectionIngestStateMachine.CompensatedStatus, outbox.Value.Status);
        Assert.True(outbox.Value.TerminalFailure);
        Assert.False(outbox.Value.PayloadStaged);
        Assert.False(outbox.Value.FilesStaged);
        Assert.False(outbox.Value.Completed);
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

    private sealed class SelectiveThrowingInspector : IInspectionFileSecurityInspector
    {
        private readonly string _throwOnFileName;
        private readonly InspectionFileSecurityInspector _inner = new(new SignatureInspectionFileMalwareScanner());

        public SelectiveThrowingInspector(string throwOnFileName)
        {
            _throwOnFileName = throwOnFileName;
        }

        public Task<InspectionFileInspectionResult> InspectAsync(IFormFile file, CancellationToken cancellationToken)
        {
            if (string.Equals(file.FileName, _throwOnFileName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("simulated inspection failure");
            }

            return _inner.InspectAsync(file, cancellationToken);
        }
    }
}
