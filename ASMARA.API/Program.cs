using ASMARA.API.Common.ExceptionHandlers;
using ASMARA.API.Extensions;
using Serilog;
using System.Security.Claims;
using ASMARA.API.Common.ExceptionHandlers;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ASMARA API");

    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.AddAsmaraSerilog();
    builder.Services.AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    }).AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddAsmaraServices(builder.Configuration);
    builder.Services.AddAsmaraMediator();
    builder.Services.AddAsmaraSecurity(builder.Configuration);
    builder.Services.AddAsmaraOpenApi();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
            policy.AllowAnyOrigin();
            policy.WithExposedHeaders("X-Correlation-Id");
        });
    });

    //Exceptions
    builder.Services.AddProblemDetails();

    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    //Middleware
    var app = builder.Build();

    app.UseCorrelationId();
    app.UseAsmaraRequestLogging();
    app.UseExceptionHandler();
    app.UseAsmaraOpenApi();
    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseUserLogContext();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "ASMARA API terminated unexpectedly");
}
finally
{
    Log.Information("ASMARA API stopped");
    Log.CloseAndFlush();
}

