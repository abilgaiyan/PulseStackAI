using PulseStack.Agents.Runtime.Observability;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Agents.Runtime;

namespace PulseStack.Tests.Workflows;
internal static class WorkflowRuntimeFactory
{
    public static AgentNodeExecutor CreateAgentExecutor()
    {
        var dispatcher =
            new RuntimeEventDispatcher();

        var runtime =
            new AgentRuntime(
                dispatcher);

        return new AgentNodeExecutor(
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

    private static List<INodeExecutor> CreateExecutors(
        AgentRuntime agentRuntime,
        WorkflowRuntime? nestedRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(agentRuntime);

        var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        executors.Add(
            new AgentNodeExecutor(
                agentRuntime));

        executors.Add(
            new PipelineNodeExecutor());

        if (nestedRuntime is not null)
        {
            executors.Add(
                new WorkflowNodeExecutor(
                    new Lazy<IWorkflowRuntime>(
                        () => nestedRuntime)));
        }

        executors.Add(
            new ConditionalNodeExecutor(
                resolver));

        executors.Add(
            new RetryNodeExecutor(
                resolver));

        executors.Add(
            new ParallelNodeExecutor(
                resolver));

        executors.Add(
            new LoopNodeExecutor(
                resolver));

        executors.Add(
            new SwitchNodeExecutor(
                resolver));

        return executors;
    }
}
