using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Tools;

namespace PulseStack.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStack(
        this IServiceCollection services)
    {
        services.TryAddSingleton<IToolRegistry, ToolRegistry>();

        // Populate registry AFTER container builds
        services.AddSingleton<IToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            var tools = sp.GetServices<ITool>();

            foreach (var tool in tools)
                registry.Register(tool);

            return registry;
        });

        return services;
    }
}