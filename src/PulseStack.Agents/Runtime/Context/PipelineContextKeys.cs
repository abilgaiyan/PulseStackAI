namespace PulseStack.Agents.Runtime.Context;

internal static class PipelineContextKeys
{
    public const string RuntimeExecutionId = "runtime:execution-id";

    public const string RuntimeBranchId = "runtime:branch-id";

    public const string RuntimeEventDispatcher = "runtime:event-dispatcher";

    public const string RuntimeAgentLifecycleManaged = "runtime:agent-lifecycle-managed";

    public static string AgentOutput(
        string agentName)
        => $"agent:{agentName}:output";

    public static string AgentError(
        string agentName)
        => $"agent:{agentName}:error";
}
