using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Common.ExceptionHandlers;

public class ProblemDetailsWriter
{
    public const string ErrorCodeKey = "errorCode";

    public static async ValueTask WriteAsync(
        IProblemDetailsService service, HttpContext httpContext, ProblemDetails problem, Exception exception, CancellationToken ct)
    {
        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        var written = await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });

        if (!written)
            await httpContext.Response.WriteAsJsonAsync(problem, ct);
    }
}
