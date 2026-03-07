using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using React_Receiver.Application.FormSchemas;

namespace React_Receiver.Mediation.Exceptions;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            RequestParsingException parsingException => new ProblemDetails
            {
                Title = "Invalid request payload",
                Detail = parsingException.Message,
                Status = StatusCodes.Status400BadRequest
            },
            FormSchemaBlobContentException => new ProblemDetails
            {
                Title = "Schema content unavailable",
                Detail = "Schema metadata exists, but the stored schema content could not be read.",
                Status = StatusCodes.Status500InternalServerError
            },
            _ => new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError
            }
        };

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
