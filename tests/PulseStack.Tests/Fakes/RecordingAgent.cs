using Microsoft.Extensions.AI;
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

    public string? Model => "test-model";

    public Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _executionOrder.Add(Name);

        context.CurrentOutput = Name;

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    Name)));
    }

    public Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        _executionOrder.Add(Name);

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    Name)));
    }

    public IAsyncEnumerable<string> StreamAsync(string input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}