using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Agents;

internal sealed class SlowAgent : IAgent
{
    public string Name => "LongRunningAnalysis";

    public async Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        await Task.Delay(
            TimeSpan.FromSeconds(10),
            cancellationToken);

        return new AgentResponse
        {
            Text = "Analysis completed."
        };
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        await Task.Delay(
            TimeSpan.FromSeconds(1),
            cancellationToken);

        yield break;
    }
}