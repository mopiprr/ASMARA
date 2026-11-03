using Serilog;
using Serilog.Events;
using System.Security.Claims;

namespace ASMARA.API.Extensions;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddAsmaraSerilog(this  WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            Serilog.Debugging.SelfLog.Enable(Console.Error);
        }

        builder.Services.AddSerilog((services, configuration) => configuration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
        );

        return builder;
    }

    public static IApplicationBuilder UseAsmaraRequestLogging(this IApplicationBuilder app)
        => app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0} ms";
                options.GetLevel = GetLevel;
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                    {
                        diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
                        diagnosticContext.Set("CorrelationId", httpContext.Response.Headers["X-Correlation-Id"].ToString());
                    };
            });

    private static LogEventLevel GetLevel(HttpContext httpContext, double elapsedMs, Exception? exception)
        => exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= StatusCodes.Status400BadRequest
                ? LogEventLevel.Warning
                : LogEventLevel.Information;
}
