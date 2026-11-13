using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ASMARA.API.Common.ExceptionHandlers;

public class ValidationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not ValidationException validationException)
            return false;

        const string prefix = "Request.";
        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName.StartsWith(prefix, StringComparison.Ordinal) ? e.PropertyName[prefix.Length..] : e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        // ValidationException yang dibuat dari string (tanpa ValidationFailure) -> tetap tampilkan pesannya.
        if (errors.Count == 0)
            errors["general"] = [validationException.Message];

        var problem = new ValidationProblemDetails(errors)
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        };
        problem.Extensions[ProblemDetailsWriter.ErrorCodeKey] = "VALIDATION_FAILED";

        await ProblemDetailsWriter.WriteAsync(problemDetailsService, httpContext, problem, exception, ct);
        return true;
    }
}