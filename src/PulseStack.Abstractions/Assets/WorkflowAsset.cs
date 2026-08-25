using System.Diagnostics.CodeAnalysis;
using PulseStack.Abstractions.Workflows.Definitions;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Workflow Asset describing reusable business flow.
/// </summary>
public sealed record WorkflowAsset : Asset
{
    [SetsRequiredMembers]
    internal WorkflowAsset(
        AssetId id,
        AssetUrn urn,
        WorkflowAssetOptions options)
        : base(AssetType.Workflow)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalized = options with
        {
            Steps = options.Steps.ToArray()
        };

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = normalized.Name,
            Description = normalized.Description,
            Tags = []
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = normalized;
        References = CollectReferences(normalized.Steps);
    }

    public WorkflowAssetOptions Options { get; }

    private static IReadOnlyCollection<AssetReference> CollectReferences(
        IReadOnlyCollection<WorkflowStepDefinition> steps)
        => steps
            .SelectMany(CollectReferences)
            .Distinct()
            .ToArray();

    private static IEnumerable<AssetReference> CollectReferences(
        WorkflowStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step switch
        {
            RunStepDefinition run => [run.Agent],

            ParallelStepDefinition parallel =>
                parallel.Steps.SelectMany(CollectReferences),

            ConditionalStepDefinition conditional =>
                CollectReferences(conditional.ThenStep)
                    .Concat(
                        conditional.ElseStep is null
                            ? []
                            : CollectReferences(conditional.ElseStep)),

            RetryStepDefinition retry =>
                CollectReferences(retry.Step),

            LoopStepDefinition loop =>
                CollectReferences(loop.Step),

            SwitchStepDefinition @switch =>
                @switch.Cases
                    .SelectMany(@case => CollectReferences(@case.Step))
                    .Concat(
                        @switch.DefaultStep is null
                            ? []
                            : CollectReferences(@switch.DefaultStep)),

            _ => []
        };
    }
}
