using ASMARA.API.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace ASMARA.API.Common.ExceptionHandlers;

public class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not AsmaraExceptions asmaraException)
            return false; 

        return true;
    }
}
