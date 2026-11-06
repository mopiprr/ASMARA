using Serilog.Context;
using System.Security.Claims;

namespace ASMARA.API.Middlewares;

public class UserLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue("employee_id") ?? "anonymous";

        using (LogContext.PushProperty("UserId", userId))
        {
            await next(context);
        }
    }
}