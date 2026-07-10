using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Runtime.Pipeline;
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
        services.AddSingleton<IStepExecutorResolver, StepExecutorResolver>();

        return services;
    }

    private static IServiceCollection AddWorkflowNodeExecutors(
        this IServiceCollection services)
    {
        services.AddSingleton<IStepExecutor, RunStepExecutor>();
        services.AddSingleton<IStepExecutor, PipelineStepExecutor>();
        services.AddSingleton<IStepExecutor, WorkflowStepExecutor>();
        services.AddSingleton<IStepExecutor, ConditionalStepExecutor>();
        services.AddSingleton<IStepExecutor, RetryStepExecutor>();
        services.AddSingleton<IStepExecutor, ParallelStepExecutor>();
        services.AddSingleton<IStepExecutor, LoopStepExecutor>();
        services.AddSingleton<IStepExecutor, SwitchStepExecutor>();

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
