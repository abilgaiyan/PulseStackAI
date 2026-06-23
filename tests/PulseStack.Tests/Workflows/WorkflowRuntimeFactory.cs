using PulseStack.Agents.Runtime.Observability;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Abstractions.Runtime.Pipeline;
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

        executors.Add(
            new ConditionalNodeExecutor(
                resolver));

        executors.Add(
            new RetryNodeExecutor(
                resolver));                

        return new WorkflowRuntime(
            executors,
            dispatcher);
    }

    public static WorkflowRuntime CreateWithNestedWorkflowSupport()
    {
        var dispatcher =
            new RuntimeEventDispatcher();

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        var nestedRuntime =
            new WorkflowRuntime(
                CreateExecutors(agentRuntime),
                dispatcher);

        return new WorkflowRuntime(
            CreateExecutors(
                agentRuntime,
                nestedRuntime),
            dispatcher);
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
                    new WorkflowRuntime(
                        CreateExecutors(agentRuntime),
                        dispatcher);

          return new WorkflowRuntime(
            CreateExecutors(
                agentRuntime,
                nestedRuntime),
            dispatcher);
    }

    private static List<INodeExecutor> CreateExecutors(
        AgentRuntime agentRuntime,
        WorkflowRuntime? workflowRuntime = null)
    {
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

        if (workflowRuntime is not null)
        {
            executors.Add(
                new WorkflowNodeExecutor(
                    workflowRuntime));
        }

        executors.Add(
            new ConditionalNodeExecutor(
                resolver));

        return executors;
    }
}