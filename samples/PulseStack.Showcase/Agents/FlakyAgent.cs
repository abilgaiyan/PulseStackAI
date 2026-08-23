using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Agents;

internal sealed class FlakyAgent : IAgent
{
    private int _attempts;

    public string Name => "TransientValidator";

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        _attempts++;

        if (_attempts < 2)
        {
            throw new InvalidOperationException(
                "Transient validation failure.");
        }

        return Task.FromResult(
            new AgentResponse
            {
                Text = "Validation succeeded."
            });
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        await Task.Delay(
            1000,
            cancellationToken);

        yield break;
    }
}