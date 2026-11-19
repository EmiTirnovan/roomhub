using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyApp.Application.Common.Cqrs;

namespace MyApp.Application.Common.DependencyInjection;

public static class CqrsRegistrationExtensions
{
    /// <summary>
    /// Scans the target assemblies for concrete ICommandHandler implementations
    /// and registers them in the Dependency Injection container with Scoped lifetime.
    /// Prevents duplicate registrations and ignores abstract or open-generic types.
    /// </summary>
    public static IServiceCollection AddHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetExecutingAssembly()];
        }

        var handlerInterfaceType = typeof(ICommandHandler<,>);

        foreach (var assembly in assemblies)
        {
            var handlerTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);

            foreach (var type in handlerTypes)
            {
                var implementedInterfaces = type.GetInterfaces();

                foreach (var iface in implementedInterfaces)
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == handlerInterfaceType)
                    {
                        // 1. Register under the interface (e.g. ICommandHandler<PublishEventCommand, Unit>)
                        services.TryAddScoped(iface, type);

                        // 2. Also register the concrete class directly for parameter injection in Minimal APIs
                        services.TryAddScoped(type);
                    }
                }
            }
        }

        return services;
    }
}
