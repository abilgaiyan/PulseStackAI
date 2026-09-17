using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Application;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Application;
using PulseStack.Agents.Runtime.Realization;
using PulseStack.Core.Runtime.Invocation.Application;
using PulseStack.Core.Runtime.Realization.Application;

namespace PulseStack.Agents.DependencyInjection;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStackAgents(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IRuntimeEventDispatcher,
                                 RuntimeEventDispatcher>();

        services.TryAddSingleton<AgentRuntime>(sp =>
            new AgentRuntime(sp.GetRequiredService<IRuntimeEventDispatcher>()));

        services.TryAddSingleton<IAgentRuntime>(sp =>
            sp.GetRequiredService<AgentRuntime>());

        services.TryAddSingleton<IAgentExecutionRuntime>(sp =>
            sp.GetRequiredService<AgentRuntime>());

        services.TryAddScoped<IAgentComposer, AgentComposer>();

        services.TryAddScoped<IApplicationRealizationChainFactory,
                              ApplicationRealizationChainFactory>();

        services.TryAddScoped<IApplicationRealizer,
                              ApplicationRealizer>();

        services.TryAddSingleton<ApplicationInvocationCoordinationAuthority>();

        services.TryAddSingleton<IApplicationInvoker, ApplicationInvoker>();

        services.TryAddScoped<IApplicationOperation, ApplicationOperation>();

        services.TryAddSingleton<PipelineRuntime>();

        services.TryAddSingleton<SequentialPipelineExecutionStrategy>(sp =>
            new SequentialPipelineExecutionStrategy(
                sp.GetRequiredService<IAgentExecutionRuntime>()));

        services.TryAddSingleton<ParallelPipelineExecutionStrategy>(sp =>
            new ParallelPipelineExecutionStrategy(
                sp.GetRequiredService<IAgentExecutionRuntime>()));

        return services;
    }
}
