using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;
using PulseStack.Abstractions.Security;
using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Serialization;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Core.Memory;
using PulseStack.Core.Resilience;
using PulseStack.Core.Tools;
using PulseStack.Core.Security;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Core.Persistence.Validation;
using PulseStack.Core.Runtime.Realization.Resolution;

namespace PulseStack.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStack(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IToolAuthorizationService, AllowAllToolAuthorizationService>();
        services.TryAddScoped<IToolExecutor, ToolExecutor>();

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

        services.AddPulseStackModelCatalog();

        services.TryAddSingleton<IWorkflowMapper, WorkflowMapper>();
        services.TryAddSingleton<IWorkflowSerializer, JsonWorkflowSerializer>();
        services.TryAddSingleton<IWorkflowDeserializer, JsonWorkflowDeserializer>();
        services.TryAddSingleton<IWorkflowValidator, WorkflowValidator>();

        // Runtime realization
        // The default resolver resolves assets registered in the current service scope.
        // Applications can replace it with a package, library, or registry-backed resolver.
        services.TryAddScoped<IAssetResolver>(sp =>
            new InMemoryAssetResolver(sp.GetServices<IAsset>()));

        return services;
    }
}
