using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Validation;

/// <summary>
/// Validates whether a declarative Agent definition has a complete exact Asset graph for realization.
/// </summary>
public interface IAgentGraphValidator
{
    ValueTask<AgentGraphValidationResult> ValidateAsync(
        AgentDefinition definition,
        CancellationToken cancellationToken = default);
}
