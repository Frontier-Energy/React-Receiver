using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.Inspections;
using React_Receiver.Controllers;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Models;
using React_Receiver.Tests.TestDoubles;
using Xunit;

namespace React_Receiver.Tests;

public sealed class InspectionIngestOutboxAdminControllerTests
{
    [Fact]
    public async Task GetSessions_ForwardsStatusAndClampedLimit()
    {
        GetInspectionIngestOutboxQuery? captured = null;
        var controller = CreateController((request, _) =>
        {
            captured = Assert.IsType<GetInspectionIngestOutboxQuery>(request);
            return Task.FromResult<object?>(Array.Empty<InspectionIngestOutboxSessionSummary>());
        });

        var response = await controller.GetSessions(new GetInspectionIngestOutboxRequest
        {
            Status = InspectionIngestStateMachine.PoisonedStatus,
            Limit = 500
        });

        Assert.NotNull(response);
        Assert.NotNull(captured);
        Assert.Equal(InspectionIngestStateMachine.PoisonedStatus, captured!.Status);
        Assert.Equal(200, captured.Limit);
    }

    [Fact]
    public async Task GetSession_ReturnsNotFound_WhenSessionMissing()
    {
        var controller = CreateController((request, _) =>
        {
            Assert.IsType<GetInspectionIngestOutboxSessionQuery>(request);
            return Task.FromResult<object?>(null);
        });

        var result = await controller.GetSession("missing");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task ReplaySession_ReturnsConflict_WhenReplayRejected()
    {
        var controller = CreateController((request, _) =>
        {
            var command = Assert.IsType<ReplayInspectionIngestOutboxSessionCommand>(request);
            Assert.True(command.Force);
            return Task.FromResult<object?>(new ReplayInspectionIngestSessionResponse(false, "blocked", new InspectionIngestOutboxSessionDetail(
                "session-1",
                "user-1",
                "name-1",
                new Dictionary<string, string>(),
                Array.Empty<InspectionIngestFileManifest>(),
                "Rejected",
                true,
                true,
                false,
                false,
                false,
                false,
                true,
                false,
                true,
                3,
                "blocked",
                null,
                null,
                null,
                null,
                null)));
        });

        var result = await controller.ReplaySession(
            "session-1",
            new ReplayInspectionIngestOutboxRequest { Force = true });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    private static InspectionIngestOutboxAdminController CreateController(Func<object, CancellationToken, Task<object?>> handler)
    {
        return new InspectionIngestOutboxAdminController(new TestSender(handler))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }
}
