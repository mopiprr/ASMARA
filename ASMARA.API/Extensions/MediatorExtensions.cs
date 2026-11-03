using ASMARA.API.Common.Behaviors;
using FluentValidation;

namespace ASMARA.API.Extensions;

public static class MediatorExtensions
{
    public static IServiceCollection AddAsmaraMediator(this IServiceCollection services)
    {
        var assembly = typeof(MediatorExtensions).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);  
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly); 

        return services;
    }
}
