namespace PulseStack.Agents.Runtime.Context;

internal static class PipelineContextKeys
{
    public static string AgentOutput(
        string agentName)
        => $"agent:{agentName}:output";

    public static string AgentError(
        string agentName)
        => $"agent:{agentName}:error";
}
