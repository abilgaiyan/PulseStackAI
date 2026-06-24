using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

internal sealed class LoopAwareFakeAgent : IAgent
{
    public string Name { get; }

    public string? Model => null;

    public LoopAwareFakeAgent(string name)
    {
        Name = name;
    }

    public Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var item =
            context.Items["CurrentItem"]
                ?.ToString();

        var output =
            $"Received: {item}";

        context.CurrentOutput = output;

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    output)));
    }

     public Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var context =
            new PipelineContext
            {
                Input = input,
                CurrentOutput = input
            };

        return RunAsync(
            context,
            cancellationToken);
    }
    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        yield return $"Received: {input}";
        await Task.CompletedTask;
    }
}