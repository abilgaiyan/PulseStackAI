using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

internal sealed class FakeAgent : IAgent
{
    private readonly Queue<string> _responses;

    public string Name { get; }

    public FakeAgent(
        string name,
        IEnumerable<string> responses)
    {
        Name = name;
        _responses = new Queue<string>(responses);
    }

    public Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var text = _responses.Dequeue();

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(ChatRole.Assistant, text)));
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        var text = _responses.Dequeue();

        yield return text;

        await Task.CompletedTask;
    }
}