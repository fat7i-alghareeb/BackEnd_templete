using System.Reflection;

using FluentValidation;

using Taxi.Application.Common.Behaviours;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            // Registration order IS execution order — first registered is outermost.
            // UnhandledExceptionBehaviour must wrap everything so failures inside the
            // validation and performance behaviours are logged with the request payload.
            cfg.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
            cfg.AddOpenBehavior(typeof(CachingBehavior<,>));

            // Open-generic IRequestPreProcessor implementations are NOT discovered by
            // RegisterServicesFromAssembly — without this line LoggingBehaviour never runs.
            cfg.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
        });

        return services;
    }
}