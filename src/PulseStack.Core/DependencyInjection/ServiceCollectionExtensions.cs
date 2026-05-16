using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Memory;
using PulseStack.Core.Resilience;
using PulseStack.Core.Tools;

namespace PulseStack.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStack(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Core framework services
        services.AddScoped<IConversationMemory, ConversationMemory>();

        // Required for HttpTool and future integrations
        services.AddHttpClient();

        services.AddPulseStackResilience();

        // Build tool registry from registered tools
        services.AddSingleton<IToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();

            var tools = sp.GetServices<ITool>();

            foreach (var tool in tools)
            {
                registry.Register(tool);
            }

            return registry;
        });

        return services;
    }
}