using Microsoft.Extensions.AI;

namespace PulseStack.Abstractions.Agents;

public interface IAgent
{
    string Name { get; }

    Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        string input,
        CancellationToken cancellationToken = default);        
}