using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Agents;

internal sealed class SlowAgent : IAgent
{
    public string Name => "LongRunningAnalysis";

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

    public async Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(10),
            cancellationToken);

        return new ChatResponse(
        [
            new ChatMessage(
                ChatRole.Assistant,
                "Analysis completed.")
        ]);
    }

     public async IAsyncEnumerable<string> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1000, cancellationToken);
        yield break;
    }
}