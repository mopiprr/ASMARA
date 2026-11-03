using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ASMARA.API.OpenApi;

public sealed class DocumentInfoTransformer(IConfiguration configuration) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "ASMARA API",
            Version = "v1", 
            Description = "Ticketing System for procurement and maintenance",
            //Contact = new OpenApiContact
            //{
            //    Name = "ASMARA Platform Team",
            //    Email = "hris-support@company.id",
            //    Url = new Uri("https://intranet.company.id/hris")
            //},
            //License = new OpenApiLicense
            //{
            //    Name = "Internal use only",
            //    Url = new Uri("https://intranet.company.id/hris/license")
            //}
        };

        var servers = configuration.GetSection("OpenApi:Servers").GetChildren()
            .Where(s => !string.IsNullOrWhiteSpace(s["Url"]))
            .Select(s => new OpenApiServer { Url = s["Url"]!, Description = s["Description"] })
            .ToList();
        if (servers.Count > 0)
            document.Servers = servers;

        return Task.CompletedTask;
    }
}