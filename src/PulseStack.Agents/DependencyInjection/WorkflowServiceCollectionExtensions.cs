using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Composition;

namespace PulseStack.Agents.DependencyInjection;

public static class WorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStackWorkflows(
        this IServiceCollection services)
    {
        services.AddSingleton<INodeExecutorResolver, NodeExecutorResolver>();

        services.AddSingleton<INodeExecutor, AgentNodeExecutor>();
        services.AddSingleton<INodeExecutor, PipelineNodeExecutor>();
        services.AddSingleton<INodeExecutor, WorkflowNodeExecutor>();
        services.AddSingleton<INodeExecutor, ConditionalNodeExecutor>();
        services.AddSingleton<INodeExecutor, RetryNodeExecutor>();
        services.AddSingleton<INodeExecutor, ParallelNodeExecutor>();
        services.AddSingleton<INodeExecutor, LoopNodeExecutor>();

        services.AddSingleton<IWorkflowRuntime, WorkflowRuntime>();

        services.AddSingleton<
            Lazy<IWorkflowRuntime>>(sp =>
                new Lazy<IWorkflowRuntime>(
                    () => sp.GetRequiredService<IWorkflowRuntime>()));

        return services;
    }
}