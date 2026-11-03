using Asmara.API.Data;
using ASMARA.API.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ASMARA.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAsmaraServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("AsmaraDB");

        services.AddDbContext<AsmaraDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssembly(
                typeof(ServiceCollectionExtensions).Assembly);
        });

        return services;
    }
}