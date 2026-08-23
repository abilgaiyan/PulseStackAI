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

        var normalizedOptions = Normalize(options);

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = normalizedOptions.Name,
            Tags = []
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = normalizedOptions;
        References = CollectReferences(normalizedOptions);
    }

    public AgentDefinitionOptions Options { get; }

    private static AgentDefinitionOptions Normalize(
        AgentDefinitionOptions options)
        => options with
        {
            Responsibilities = options.Responsibilities.ToArray(),
            Knowledge = options.Knowledge.ToArray(),
            Tools = options.Tools.ToArray(),
            Policies = options.Policies.ToArray()
        };

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

        return references.Distinct().ToArray();
    }
}
