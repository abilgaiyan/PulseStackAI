using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Agents; 

internal sealed class SuccessfulAgent : IAgent
{
    private readonly string _response;

    public SuccessfulAgent(
        string name,
        string response)
    {
        Name = name;
        _response = response;
    }

    public string Name { get; }

    public string Model =>
        "demo";

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new AgentResponse
            {
                Text = _response,
                Model = Model
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