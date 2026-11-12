
using ASMARA.API.Common.ExceptionHandlers;
using ASMARA.API.Common.Json;
using ASMARA.API.Data;
using ASMARA.API.Extensions;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ASMARA API");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.AddAsmaraSerilog();

    // Controllers
    builder.Services.AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    });

    // Application Services
    builder.Services.AddAsmaraServices(builder.Configuration);

    // MediatR
    builder.Services.AddAsmaraMediator();

    // Security (JWT & PasswordHasher)
    builder.Services.AddAsmaraSecurity(builder.Configuration);

    // OpenAPI / Swagger
    builder.Services.AddAsmaraOpenApi();

    // CORS
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:5173"];

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins);
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
            policy.AllowCredentials();

            policy.WithExposedHeaders("X-Correlation-Id");
        });
    });

    // Exception Handling
    builder.Services.AddProblemDetails();

    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Build Application
    var app = builder.Build();

    // Development Database Seeding
    if (app.Environment.IsDevelopment())
    {
        await using var scope = app.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AsmaraDbContext>();

        // Check database connection
        var canConnect = await db.Database.CanConnectAsync();

        if (!canConnect)
        {
            throw new InvalidOperationException(
                "Unable to connect to ASMARA database.");
        }

        Log.Information("ASMARA database connection successful.");

        // Seed development data
        await DevelopmentDataSeeder.SeedAsync(db);

        Log.Information("Development data seeding completed.");
    }

    // Middleware Pipeline
    app.UseCorrelationId();

    app.UseAsmaraRequestLogging();

    app.UseExceptionHandler();

    app.UseAsmaraOpenApi();

    app.UseHttpsRedirection();

    app.UseCors();

    // Authentication & Authorization
    app.UseAuthentication();

    app.UseUserLogContext();

    app.UseAuthorization();

    // Map Endpoints
    app.MapControllers();

    // Run Application
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
