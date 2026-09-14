using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Realization.Application;

namespace PulseStack.Core.Runtime.Realization.Application;

/// <summary>
/// Coordinates realization of one accepted AI Asset graph into the existing Workflow runtime representation.
/// </summary>
public sealed class ApplicationRealizer : IApplicationRealizer
{
    private readonly IApplicationRealizationChainFactory _chainFactory;

    public ApplicationRealizer(IApplicationRealizationChainFactory chainFactory)
    {
        ArgumentNullException.ThrowIfNull(chainFactory);
        _chainFactory = chainFactory;
    }

    public async Task<ApplicationRealizationResult> RealizeAsync(
        AIAssetGraph graph,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.RootKey.Type is AssetType.Library or AssetType.Package)
        {
            return new ApplicationRealizationResult.UnsupportedRoot(
                new ApplicationRealizationUnsupportedRootContext(graph.RootKey));
        }

        var rootAsset = graph.Nodes
            .Single(node => node.DefinitionKey == graph.RootKey)
            .Asset;

        if (rootAsset is not ProjectAsset project)
        {
            throw new InvalidOperationException(
                "A Project graph root must contain a concrete ProjectAsset.");
        }

        var entryWorkflow = project.Options.EntryWorkflow;
        var resolver = new GraphBackedAssetResolver(graph);
        var resolvedEntry = await resolver
            .ResolveAsync(entryWorkflow, cancellationToken)
            .ConfigureAwait(false);

        if (resolvedEntry is null)
        {
            return new ApplicationRealizationResult.EntryWorkflowUnresolved(
                new ApplicationRealizationEntryWorkflowUnresolvedContext(
                    graph.RootKey,
                    entryWorkflow));
        }

        if (resolvedEntry is not WorkflowAsset workflowAsset)
        {
            return new ApplicationRealizationResult.EntryWorkflowTypeIncoherent(
                new ApplicationRealizationEntryWorkflowTypeIncoherentContext(
                    graph.RootKey,
                    entryWorkflow));
        }

        var composer = _chainFactory.Create(resolver);
        var workflow = await composer
            .ComposeAsync(workflowAsset, cancellationToken)
            .ConfigureAwait(false);

        return new ApplicationRealizationResult.Success(workflow);
    }
}
