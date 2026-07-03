using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Agents.Runtime.Composition;

namespace PulseStack.Agents.DependencyInjection;

public static class WorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddPulseStackWorkflows(
        this IServiceCollection services)
    {
        services.AddWorkflowRuntimeInfrastructure();
        services.AddWorkflowNodeExecutors();
        services.AddLazyWorkflowRuntime();

        return services;
    }

    private static IServiceCollection AddWorkflowRuntimeInfrastructure(
        this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowRuntime, WorkflowRuntime>();
        services.AddSingleton<INodeExecutorResolver, NodeExecutorResolver>();

        return services;
    }

    private static IServiceCollection AddWorkflowNodeExecutors(
        this IServiceCollection services)
    {
        services.AddSingleton<INodeExecutor, AgentNodeExecutor>();
        services.AddSingleton<INodeExecutor, PipelineNodeExecutor>();
        services.AddSingleton<INodeExecutor, WorkflowNodeExecutor>();
        services.AddSingleton<INodeExecutor, ConditionalNodeExecutor>();
        services.AddSingleton<INodeExecutor, RetryNodeExecutor>();
        services.AddSingleton<INodeExecutor, ParallelNodeExecutor>();
        services.AddSingleton<INodeExecutor, LoopNodeExecutor>();
        services.AddSingleton<INodeExecutor, SwitchNodeExecutor>();

        return services;
    }

    private static IServiceCollection AddLazyWorkflowRuntime(
        this IServiceCollection services)
    {
        services.AddSingleton<
            Lazy<IWorkflowRuntime>>(sp =>
                new Lazy<IWorkflowRuntime>(
                    () => sp.GetRequiredService<IWorkflowRuntime>()));

        return services;
    }
}
