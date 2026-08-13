using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Agent Asset describing a reusable business worker.
/// </summary>
public sealed record AgentDefinition : Asset
{
    [SetsRequiredMembers]
    internal AgentDefinition(
        AssetId id,
        AssetUrn urn,
        AgentDefinitionOptions options)
        : base(AssetType.Agent)
    {
        ArgumentNullException.ThrowIfNull(options);

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = options.Name,
            Tags = []
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = options;
        References = CollectReferences(options);
    }

    public AgentDefinitionOptions Options { get; }

    private static IReadOnlyCollection<AssetReference> CollectReferences(
        AgentDefinitionOptions options)
    {
        var references = new List<AssetReference>();

        if (options.Model is not null)
        {
            references.Add(options.Model);
        }

        if (options.Prompt is not null)
        {
            references.Add(options.Prompt);
        }

        references.AddRange(options.Knowledge);
        references.AddRange(options.Tools);

        if (options.Memory is not null)
        {
            references.Add(options.Memory);
        }

        references.AddRange(options.Policies);

        return references.ToArray();
    }
}
