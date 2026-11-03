using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Common.ExceptionHandlers;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled {ExceptionType} while processing {Method} {Path}",
            exception.GetType().Name, httpContext.Request.Method, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = "Please contact support and quote the traceId."
        };
        problem.Extensions[ProblemDetailsWriter.ErrorCodeKey] = "INTERNAL_ERROR";

        await ProblemDetailsWriter.WriteAsync(problemDetailsService, httpContext, problem, exception, ct);
        return true;
    }
}
