using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Mediation.Exceptions;
using Xunit;

namespace React_Receiver.Tests;

public sealed class ApiExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_ReturnsBadRequestProblem_ForParsingErrors()
    {
        var problemDetailsService = new CapturingProblemDetailsService();
        var handler = new ApiExceptionHandler(problemDetailsService);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext,
            new RequestParsingException("Invalid payload JSON."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal("Invalid request payload", problemDetailsService.LastProblem?.Title);
        Assert.Equal("Invalid payload JSON.", problemDetailsService.LastProblem?.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsServerProblem_ForSchemaBlobErrors()
    {
        var problemDetailsService = new CapturingProblemDetailsService();
        var handler = new ApiExceptionHandler(problemDetailsService);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext,
            new FormSchemaBlobContentException("missing blob"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("Schema content unavailable", problemDetailsService.LastProblem?.Title);
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? LastProblem { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            LastProblem = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            LastProblem = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }
}
