namespace PulseStack.Abstractions.Agents;
public interface IAgentRuntime
{
    Task<AgentResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        string input,
        CancellationToken cancellationToken = default);
}
