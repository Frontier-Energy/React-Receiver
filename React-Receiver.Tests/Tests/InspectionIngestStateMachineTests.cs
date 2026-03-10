using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Models;
using Xunit;

namespace React_Receiver.Tests;

public sealed class InspectionIngestStateMachineTests
{
    [Fact]
    public void NormalizeRequest_FillsMissingSessionIdAndQueryParams()
    {
        var request = new ReceiveInspectionRequest(null, "user-1", "name-1", null, null);

        var normalized = InspectionIngestStateMachine.NormalizeRequest(request);

        Assert.False(string.IsNullOrWhiteSpace(normalized.SessionId));
        Assert.NotNull(normalized.QueryParams);
        Assert.Empty(normalized.QueryParams!);
    }

    [Fact]
    public void ValidateEquivalentRequest_Throws_WhenNormalizedPayloadDiffers()
    {
        var request = new ReceiveInspectionRequest(
            "session-1",
            "user-1",
            "name-1",
            new Dictionary<string, string> { ["a"] = "b" },
            null);
        var manifest = new[]
        {
            new InspectionIngestFileManifest("a.txt", "session-1-a.txt", "text/plain", 12)
        };
        var existing = InspectionIngestStateMachine.CreateOutboxEntity(request, manifest, DateTimeOffset.UtcNow);
        existing.Name = "other-name";

        var exception = Assert.Throws<DuplicateInspectionSessionException>(() =>
            InspectionIngestStateMachine.ValidateEquivalentRequest(existing, request, manifest));

        Assert.Contains("already exists with different normalized payload", exception.Message);
    }

    [Fact]
    public void MarkCompensated_ClearsInFlightFlags()
    {
        var entity = new InspectionIngestOutboxEntity
        {
            PayloadStaged = true,
            FilesStaged = true,
            FilesVerified = true,
            MetadataWritten = true,
            QueueMessageSent = true,
            Completed = true,
            Processing = true,
            LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(1),
            NextAttemptAtUtc = DateTimeOffset.UtcNow,
            LastError = "old"
        };

        InspectionIngestStateMachine.MarkCompensated(entity, new InvalidOperationException("failure"));

        Assert.False(entity.PayloadStaged);
        Assert.False(entity.FilesStaged);
        Assert.False(entity.FilesVerified);
        Assert.False(entity.MetadataWritten);
        Assert.False(entity.QueueMessageSent);
        Assert.False(entity.Completed);
        Assert.False(entity.Processing);
        Assert.Null(entity.LockedUntilUtc);
        Assert.Null(entity.NextAttemptAtUtc);
        Assert.Equal("Compensated", entity.Status);
        Assert.Equal("failure", entity.LastError);
    }

    [Fact]
    public void MarkRetryScheduled_IncrementsRetryCount_AndUsesConfiguredBackoff()
    {
        var entity = new InspectionIngestOutboxEntity
        {
            RetryCount = 8,
            Processing = true,
            LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(1)
        };
        var now = DateTimeOffset.UtcNow;

        InspectionIngestStateMachine.MarkRetryScheduled(entity, new InvalidOperationException("boom"), now, poisonThreshold: 10);

        Assert.Equal(9, entity.RetryCount);
        Assert.False(entity.Processing);
        Assert.Null(entity.LockedUntilUtc);
        Assert.Equal("PendingRetry", entity.Status);
        Assert.Equal("boom", entity.LastError);
        Assert.Equal(now.AddSeconds(256), entity.NextAttemptAtUtc);
    }

    [Fact]
    public void MarkRetryScheduled_MarksPoisoned_WhenThresholdReached()
    {
        var entity = new InspectionIngestOutboxEntity
        {
            RetryCount = 2,
            PayloadStaged = true,
            FilesStaged = true,
            Processing = true,
            LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(1)
        };
        var now = DateTimeOffset.UtcNow;

        InspectionIngestStateMachine.MarkRetryScheduled(entity, new InvalidOperationException("boom"), now, poisonThreshold: 3);

        Assert.Equal(3, entity.RetryCount);
        Assert.True(entity.TerminalFailure);
        Assert.Equal(InspectionIngestStateMachine.PoisonedStatus, entity.Status);
        Assert.Equal(now, entity.PoisonedAtUtc);
        Assert.Null(entity.NextAttemptAtUtc);
    }

    [Fact]
    public void IsPending_RequiresReadyUnlockedStagedEntity()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new InspectionIngestOutboxEntity
        {
            PayloadStaged = true,
            FilesStaged = true,
            NextAttemptAtUtc = now.AddSeconds(-1),
            LockedUntilUtc = now.AddSeconds(-1)
        };

        Assert.True(InspectionIngestStateMachine.IsPending(entity, now));

        entity.Processing = true;
        Assert.False(InspectionIngestStateMachine.IsPending(entity, now));
    }

    [Fact]
    public void MarkFilesVerified_UpdatesVerificationState()
    {
        var entity = new InspectionIngestOutboxEntity
        {
            PayloadStaged = true,
            FilesStaged = true
        };
        var now = DateTimeOffset.UtcNow;

        InspectionIngestStateMachine.MarkFilesVerified(entity, now);

        Assert.True(entity.FilesVerified);
        Assert.Equal("FilesVerified", entity.Status);
        Assert.Equal(now, entity.NextAttemptAtUtc);
    }

    [Fact]
    public void MarkReplayQueued_ClearsTerminalFailureAndSchedulesImmediateRetry()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new InspectionIngestOutboxEntity
        {
            PayloadStaged = true,
            FilesStaged = true,
            TerminalFailure = true,
            Status = InspectionIngestStateMachine.PoisonedStatus,
            Processing = true,
            LockedUntilUtc = now.AddMinutes(1),
            PoisonedAtUtc = now
        };

        InspectionIngestStateMachine.MarkReplayQueued(entity, now, force: true);

        Assert.False(entity.TerminalFailure);
        Assert.False(entity.Processing);
        Assert.Null(entity.LockedUntilUtc);
        Assert.Null(entity.PoisonedAtUtc);
        Assert.Equal(now, entity.NextAttemptAtUtc);
        Assert.Equal(InspectionIngestStateMachine.ReplayQueuedStatus, entity.Status);
    }
}
