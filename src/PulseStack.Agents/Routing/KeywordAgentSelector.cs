using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Agents.Routing;

namespace PulseStack.Agents.Routing;

public sealed class KeywordAgentSelector
    : IAgentSelector
{
    private readonly Dictionary<string, string> _routes;

    public KeywordAgentSelector(
        Dictionary<string, string> routes)
    {
        _routes = routes;
    }

    public ValueTask<IAgent> SelectAsync(
        PipelineContext context,
        IReadOnlyCollection<IAgent> agents,
        CancellationToken cancellationToken = default)
    {
        var input =
            context.Input ?? string.Empty;

        foreach (var route in _routes)
        {
            if (input.Contains(
                    route.Key,
                    StringComparison.OrdinalIgnoreCase))
            {
                var agent =
                    agents.FirstOrDefault(
                        x => x.Name.Equals(
                            route.Value,
                            StringComparison.OrdinalIgnoreCase));

                if (agent is not null)
                {
                    return ValueTask.FromResult(agent);
                }
            }
        }

        return ValueTask.FromResult(agents.First());
    }
}
