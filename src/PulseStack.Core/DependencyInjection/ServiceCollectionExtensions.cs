using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Serialization;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Security;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Assets;
using PulseStack.Core.Chat;
using PulseStack.Core.Memory;
using PulseStack.Core.Persistence.Mapping;
using PulseStack.Core.Persistence.Serialization;
using PulseStack.Core.Persistence.Validation;
using PulseStack.Core.Resilience;
using PulseStack.Core.Runtime.Realization;
using PulseStack.Core.Runtime.Realization.Binding;
using PulseStack.Core.Runtime.Realization.Composition;
using PulseStack.Core.Runtime.Realization.Resolution;
using PulseStack.Core.Security;
using PulseStack.Core.Tools;

namespace PulseStack.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStack(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IToolAuthorizationService, AllowAllToolAuthorizationService>();
        services.TryAddScoped<IToolExecutor, ToolExecutor>();
        services.AddScoped<IConversationMemory, ConversationMemory>();
        services.TryAddSingleton<IConversationMemoryFactory, ConversationMemoryFactory>();
        services.AddHttpClient();
        services.AddPulseStackResilience();

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
        services.TryAddSingleton<ModelAssetFactory>();
        services.TryAddSingleton<PromptAssetFactory>();
        services.TryAddSingleton<ToolAssetFactory>();
        services.TryAddSingleton<KnowledgeAssetFactory>();
        services.TryAddSingleton<MemoryAssetFactory>();
        services.TryAddSingleton<PolicyAssetFactory>();
        services.TryAddSingleton<WorkflowAssetFactory>();
        services.TryAddSingleton<IChatClientFactoryRegistry>(sp =>
            new ChatClientFactoryRegistry(
                sp.GetServices<ChatClientFactoryRegistration>()));
        services.TryAddSingleton<IProviderResolver, Providers.ProviderResolver>();
        services.TryAddSingleton<ModelRealizer>();
        services.TryAddSingleton<PromptRealizer>();
        services.TryAddSingleton<IToolBindingResolver, ToolBindingResolver>();
        services.TryAddSingleton<IKnowledgeBindingResolver, KnowledgeBindingResolver>();
        services.TryAddSingleton<IMemoryBindingResolver, MemoryBindingResolver>();
        services.TryAddSingleton<IPolicyBindingResolver, PolicyBindingResolver>();
        services.TryAddSingleton<IConditionBindingResolver, ConditionBindingResolver>();
        services.TryAddScoped<IWorkflowComposer, WorkflowComposer>();

        services.TryAddSingleton<IWorkflowMapper, WorkflowMapper>();
        services.TryAddSingleton<IWorkflowSerializer, JsonWorkflowSerializer>();
        services.TryAddSingleton<IWorkflowDeserializer, JsonWorkflowDeserializer>();
        services.TryAddSingleton<IWorkflowValidator, WorkflowValidator>();

        services.TryAddScoped<IAssetResolver>(sp =>
            new InMemoryAssetResolver(sp.GetServices<IAsset>()));

        return services;
    }
}
