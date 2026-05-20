using Microsoft.Extensions.AI;

namespace PulseStack.Abstractions.Agents;

public interface IAgentRuntime
{
    Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        string input,
        CancellationToken cancellationToken = default);
}
