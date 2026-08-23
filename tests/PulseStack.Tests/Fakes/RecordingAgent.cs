using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

internal sealed class RecordingAgent : IAgent
{
    private readonly List<string> _executionOrder;

    public RecordingAgent(
        string name,
        List<string> executionOrder)
    {
        Name = name;
        _executionOrder = executionOrder;
    }

    public string Name { get; }

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        _executionOrder.Add(Name);

        return Task.FromResult(
            new AgentResponse
            {
                Text = Name
            });
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        _executionOrder.Add(Name);

        yield return Name;

        await Task.CompletedTask;
    }
}