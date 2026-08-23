using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

public sealed class ModelAwareFakeAgent : IAgent
{
    private readonly string _response;
    private readonly string _model;

    public ModelAwareFakeAgent(
        string name,
        string response,
        string model)
    {
        Name = name;
        _response = response;
        _model = model;
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
                Text = _response,
                Model = _model
            });
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        yield return _response;

        await Task.CompletedTask;
    }
}