using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

public sealed class FakeAgent : IAgent
{
    private readonly string _response;
    
    public FakeAgent(
        string name,
        string response)
    {
        Name = name;
        _response = response;
    }

    public string Name { get; }

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new AgentResponse
            {
                Text = _response
            });
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        yield return _response;

        await Task.CompletedTask;
    }
}