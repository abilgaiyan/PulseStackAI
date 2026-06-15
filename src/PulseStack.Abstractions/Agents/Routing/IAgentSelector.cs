namespace PulseStack.Abstractions.Agents.Routing;

public interface IAgentSelector
{
    ValueTask<IAgent> SelectAsync(
        PipelineContext context,
        IReadOnlyCollection<IAgent> agents,
        CancellationToken cancellationToken = default);
}
