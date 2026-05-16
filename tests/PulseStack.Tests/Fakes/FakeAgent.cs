using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

public sealed class FakeAgent : IAgent
{
    private readonly string _response;

    public FakeAgent(
        string name,
        string response,
        string? model = null)
    {
        Name = name;
        _response = response;
        Model = model;
    }

    public string Name { get; }

    public string? Model { get; }

    public Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

        return RunAsync(
            context,
            cancellationToken);
    }

    public Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        context.CurrentOutput = _response;

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    _response)));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        yield return _response;

        await Task.CompletedTask;
    }
}
