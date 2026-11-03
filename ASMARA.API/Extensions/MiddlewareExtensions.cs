using ASMARA.API.Middlewares;

namespace ASMARA.API.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationMiddleware>();

    public static IApplicationBuilder UseUserLogContext(this IApplicationBuilder builder)
        => builder.UseMiddleware<UserLogContextMiddleware>();
}
