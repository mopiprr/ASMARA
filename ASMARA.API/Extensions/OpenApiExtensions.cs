using ASMARA.API.OpenApi;

namespace ASMARA.API.Extensions;

public static class OpenApiExtensions
{
    public static IServiceCollection AddAsmaraOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<DocumentInfoTransformer>();
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<AuthorizeOperationTransformer>();
        });
        return services;
    }

    public static WebApplication UseAsmaraOpenApi(this WebApplication app)
    {
        var enabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled");
        if (!enabled)
            return app;

        app.MapOpenApi().AllowAnonymous();
        app.UseSwaggerUI(options =>        
        {
            options.SwaggerEndpoint("/openapi/v1.json", "ASMARA API");
        });
        return app;
    }
}
