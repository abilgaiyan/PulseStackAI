using PulseStack.Agents.Runtime.Observability;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime;

namespace PulseStack.Tests.Workflows;
internal static class WorkflowRuntimeFactory
{
    public static RunStepExecutor CreateAgentExecutor()
    {
        var dispatcher =
            new RuntimeEventDispatcher();

        var runtime =
            new AgentRuntime(
                dispatcher);

        return new RunStepExecutor(
            runtime);
    }

    public static WorkflowRuntime Create()
    {
        var dispatcher =
            new RuntimeEventDispatcher();

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        return CreateRuntime(
            dispatcher,
            agentRuntime);
    }

    public static WorkflowRuntime CreateWithNestedWorkflowSupport()
    {
        var dispatcher =
            new RuntimeEventDispatcher();

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        var nestedRuntime =
            CreateRuntime(
                dispatcher,
                agentRuntime);

        return CreateRuntime(
            dispatcher,
            agentRuntime,
            nestedRuntime);
    }

    public static WorkflowRuntime Create(
        IRuntimeObserver observer)
    {
        var dispatcher =
            new RuntimeEventDispatcher(
                observer);

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        var nestedRuntime =
            CreateRuntime(
                dispatcher,
                agentRuntime);

        return CreateRuntime(
            dispatcher,
            agentRuntime,
            nestedRuntime);
    }

    private static WorkflowRuntime CreateRuntime(
        RuntimeEventDispatcher dispatcher,
        AgentRuntime agentRuntime,
        WorkflowRuntime? nestedRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(agentRuntime);

        return new WorkflowRuntime(
            CreateExecutors(
                agentRuntime,
                nestedRuntime),
            dispatcher);
    }

    private static List<IStepExecutor> CreateExecutors(
        AgentRuntime agentRuntime,
        WorkflowRuntime? nestedRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(agentRuntime);

        var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        executors.Add(
            new RunStepExecutor(
                agentRuntime));

        executors.Add(
            new PipelineStepExecutor());

        if (nestedRuntime is not null)
        {
            executors.Add(
                new WorkflowStepExecutor(
                    new Lazy<IWorkflowRuntime>(
                        () => nestedRuntime)));
        }

        executors.Add(
            new ConditionalStepExecutor(
                resolver));

        executors.Add(
            new RetryStepExecutor(
                resolver));

        executors.Add(
            new ParallelStepExecutor(
                resolver));

        executors.Add(
            new LoopStepExecutor(
                resolver));

        executors.Add(
            new SwitchStepExecutor(
                resolver));

        return executors;
    }
}
