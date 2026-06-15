using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Agents;

public interface IAgent : IPipelineNode
{
    string Name { get; }
    string? Model { get; }

    Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default);

    Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default);        

    IAsyncEnumerable<string> StreamAsync(
        string input,
        CancellationToken cancellationToken = default);        
}