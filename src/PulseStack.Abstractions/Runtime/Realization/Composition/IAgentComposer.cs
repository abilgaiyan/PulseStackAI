using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Composition;

/// <summary>
/// Composes a declarative Agent Asset into a runtime Agent.
/// </summary>
public interface IAgentComposer
{
    /// <summary>
    /// Resolves the Agent definition and composes its runtime representation.
    /// </summary>
    /// <param name="definition">The declarative Agent definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The composed runtime Agent.</returns>
    Task<IAgent> ComposeAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default);
}
