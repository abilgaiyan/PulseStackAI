using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

/// <summary>
/// Creates Agent Assets from declarative Agent definition options.
/// </summary>
public sealed class AgentDefinitionFactory
{
    public AgentDefinition Create(AgentDefinitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Goal);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Role);

        var id = AssetId.New();

        return new AgentDefinition(
            id,
            new AssetUrn($"urn:pulsestack:agent:{id}"),
            options);
    }
}
