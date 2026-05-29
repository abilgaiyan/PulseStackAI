using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Agents;

internal sealed class FlakyAgent : IAgent
{
    private int _attempts;

    public string Name => "TransientValidator";

    public string Model => "demo";

    public Task<ChatResponse> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        return RunAsync(
            new PipelineContext
            {
                Input = input,
                CurrentOutput = input
            },
            cancellationToken);
    }

    public Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        _attempts++;

        if (_attempts < 2)
        {
            throw new InvalidOperationException(
                "Transient validation failure.");
        }

        return Task.FromResult(
            new ChatResponse(
                new[]
                {
                    new ChatMessage(
                        ChatRole.Assistant,
                        "Validation succeeded.")
                }));
    }

    public async IAsyncEnumerable<string> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1000, cancellationToken);
        yield break;
    }
}