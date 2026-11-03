using ASMARA.API.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Common.ExceptionHandlers;

public class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        if (exception is not AsmaraExceptions asmaraException)
            return false;

        logger.LogWarning(
            exception,
            "Domain exception occurred: {ErrorCode}",
            asmaraException.ErrorCode);

        var problem = new ProblemDetails
        {
            Status = asmaraException.StatusCode,
            Title = "Application error",
            Detail = asmaraException.Message
        };

        problem.Extensions[ProblemDetailsWriter.ErrorCodeKey] =
            asmaraException.ErrorCode;

        await ProblemDetailsWriter.WriteAsync(
            problemDetailsService,
            httpContext,
            problem,
            exception,
            ct);

        return true;
    }
}