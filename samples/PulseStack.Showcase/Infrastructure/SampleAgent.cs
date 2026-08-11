using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Infrastructure;

public sealed class SampleAgent : IAgent
{
    private readonly string _response;

    public SampleAgent(
        string name,
        string response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        _response = response;
    }

    public string Name { get; }

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        return Task.FromResult(
            new AgentResponse
            {
                Text = _response
            });
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        cancellationToken.ThrowIfCancellationRequested();

        yield return _response;

        await Task.CompletedTask;
    }
}