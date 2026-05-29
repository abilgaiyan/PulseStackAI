using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PulseStack.Agents.Runtime.Observability;

public static class RuntimeObserverServiceCollectionExtensions
{
    public static IServiceCollection AddConsoleRuntimeObserver(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IRuntimeObserver, ConsoleRuntimeObserver>());

        services.TryAddSingleton<CompositeRuntimeObserver>();

        return services;
    }

    public static IServiceCollection AddOpenTelemetryRuntimeObserver(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IRuntimeObserver, OpenTelemetryRuntimeObserver>());

        services.TryAddSingleton<CompositeRuntimeObserver>();

        return services;
    }
}
