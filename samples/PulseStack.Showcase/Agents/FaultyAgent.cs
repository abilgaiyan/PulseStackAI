using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Showcase.Agents; 

internal sealed class FaultyAgent : IAgent
{
    public string Name => "ComplianceValidator";

    public async Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1000, cancellationToken);

        throw new InvalidOperationException(
                "Compliance validation failed.");
    }
    
    public async IAsyncEnumerable<string> StreamAsync(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1000, cancellationToken);
        yield break;
    }
}