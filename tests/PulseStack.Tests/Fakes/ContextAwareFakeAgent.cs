using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Tests.Fakes;

internal sealed class ContextAwareFakeAgent : IAgent
{
    public string Name { get; }

    public string? Model => null;

    public ContextAwareFakeAgent(
        string name)
    {
        Name = name;
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

    public Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var response =
            $"Received: {context.CurrentOutput}";

        context.CurrentOutput = response;

        return Task.FromResult(
            new ChatResponse(
                new ChatMessage(
                    ChatRole.Assistant,
                    response)));
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