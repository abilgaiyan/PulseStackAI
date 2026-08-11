using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;
namespace PulseStack.Tests.Fakes;

internal sealed class LoopAwareFakeAgent :
    IAgent,
    IRuntimeAgent
{
    public string Name { get; }

    public LoopAwareFakeAgent(string name)
    {
        Name = name;
    }

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            new AgentResponse
            {
                Text = input
            });
    }

    async Task<AgentResponse> IRuntimeAgent.RunAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var item =
            context.Items["CurrentItem"]?.ToString();

        var output =
            $"Received: {item}";

        context.CurrentOutput = output;

        return new AgentResponse
        {
            Text = output
        };
    }

    public async IAsyncEnumerable<string> StreamAsync(
        string input,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        yield return $"Received: {input}";

        await Task.CompletedTask;
    }
}