using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;

namespace PulseStack.Tests.Fakes;
internal sealed class ContextAwareFakeAgent : IAgent, IRuntimeAgent
{
    public string Name { get; }

    public ContextAwareFakeAgent(string name)
    {
        Name = name;
    }

    public Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response =
            $"Received: {input}";

        return Task.FromResult(
            new AgentResponse
            {
                Text = response
            });
    }

    async Task<AgentResponse> IRuntimeAgent.RunAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
         var response =
            $"Received: {context.CurrentOutput}";

        context.CurrentOutput = response;

        return new AgentResponse
        {
            Text = response
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