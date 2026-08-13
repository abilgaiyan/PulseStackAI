using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Realization;

namespace PulseStack.Agents.DependencyInjection;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStackAgents(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IRuntimeEventDispatcher,
                                 RuntimeEventDispatcher>();

        services.TryAddSingleton<IAgentRuntime, AgentRuntime>();

        services.TryAddScoped<IAgentComposer, AgentComposer>();

        services.TryAddSingleton<PipelineRuntime>();

        services.TryAddSingleton<SequentialPipelineExecutionStrategy>();

        services.TryAddSingleton<ParallelPipelineExecutionStrategy>();

        return services;
    }
}
