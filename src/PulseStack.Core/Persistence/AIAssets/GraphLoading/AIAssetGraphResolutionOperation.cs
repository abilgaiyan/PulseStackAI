using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// Invocation-local resolution and identity authority used by aggregate graph loading.
/// This component does not enumerate descendants, recurse aggregate boundaries, or interpret cycles.
/// </summary>
internal sealed class AIAssetGraphResolutionOperation
{
    private readonly object gate = new();
    private readonly IPersistentAIAssetResolver resolver;
    private readonly AssetDefinitionKey rootKey;
    private readonly Dictionary<AssetDefinitionKey, ResolutionState> resolutions = [];
    private readonly Dictionary<AssetDefinitionKey, AssetUrn> assertedUrns = [];
    private readonly Dictionary<AssetUrn, AssetDefinitionKey> establishedLineages = [];
    private readonly Dictionary<AssetDefinitionKey, List<PendingAssertion>> pendingAssertions = [];
    private readonly List<AIAssetGraphNode> nodes = [];
    private readonly List<AIAssetGraphRelationship> relationships = [];

    private AIAssetGraphLoadResult? terminalFailure;

    internal AIAssetGraphResolutionOperation(
        IPersistentAIAssetResolver resolver,
        AssetDefinitionKey rootKey)
    {
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        this.rootKey = rootKey;
    }

    internal AssetDefinitionKey RootKey => rootKey;

    internal AIAssetGraphLoadResult? TerminalFailure
    {
        get
        {
            lock (gate)
            {
                return terminalFailure;
            }
        }
    }

    internal IReadOnlyList<AIAssetGraphNode> MaterializedNodes
    {
        get
        {
            lock (gate)
            {
                return new ReadOnlyCollection<AIAssetGraphNode>(nodes.ToArray());
            }
        }
    }

    internal IReadOnlyList<AIAssetGraphRelationship> ObservedRelationships
    {
        get
        {
            lock (gate)
            {
                return new ReadOnlyCollection<AIAssetGraphRelationship>(relationships.ToArray());
            }
        }
    }

    internal async ValueTask<AIAssetGraphLoadResult?> ResolveRootAsync(
        CancellationToken cancellationToken = default)
    {
        ResolutionState state;
        lock (gate)
        {
            if (terminalFailure is not null)
            {
                return terminalFailure;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!resolutions.TryGetValue(rootKey, out state))
            {
                var task = StartRootResolution(cancellationToken);
                state = ResolutionState.InProgress(task, null, null);
                resolutions.Add(rootKey, state);
            }
        }

        if (state.Asset is not null || state.Failure is not null)
        {
            return state.Failure;
        }

        var result = await state.ResolutionTask!.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (terminalFailure is not null)
            {
                return terminalFailure;
            }

            var current = resolutions[rootKey];
            if (current.Asset is not null || current.Failure is not null)
            {
                return current.Failure;
            }

            switch (result)
            {
                case AIAssetResolutionResult.Resolved resolved:
                    EnsureResolvedIdentity(rootKey, resolved.Asset, "root");
                    CommitResolved(rootKey, resolved.Asset);
                    return null;

                case AIAssetResolutionResult.DefinitionNotPublished:
                {
                    var failure = new AIAssetGraphLoadResult.RootDefinitionUnavailable(
                        new AIAssetGraphRootDefinitionUnavailableContext(rootKey));
                    CommitFailure(rootKey, failure);
                    return failure;
                }

                default:
                    throw UnexpectedResolverOutcome(result, "exact-key root resolution");
            }
        }
    }

    internal async ValueTask<AIAssetGraphLoadResult?> ProcessRelationshipAsync(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(relationship);

        lock (gate)
        {
            if (terminalFailure is not null)
            {
                return terminalFailure;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (path.RootKey != rootKey)
        {
            throw new ArgumentException(
                "The relationship path must belong to this resolution operation's root key.",
                nameof(path));
        }

        EnsurePathIdentifiesRelationship(path, relationship);

        ResolutionState? stateToAwait = null;
        lock (gate)
        {
            if (terminalFailure is not null)
            {
                return terminalFailure;
            }

            EnsureMaterializedSource(relationship.SourceKey);
            relationships.Add(relationship);

            var targetKey = AssetDefinitionKey.From(relationship.TargetReference);

            if (resolutions.TryGetValue(targetKey, out var known))
            {
                if (known.Failure is not null)
                {
                    terminalFailure = known.Failure;
                    return terminalFailure;
                }

                if (known.Asset is not null)
                {
                    var identityFailure = CheckKnownResolvedIdentity(path, relationship, known.Asset);
                    if (identityFailure is not null)
                    {
                        terminalFailure = identityFailure;
                    }

                    return terminalFailure;
                }

                var assertionFailure = CheckOperationLocalAssertions(path, relationship, targetKey);
                if (assertionFailure is not null)
                {
                    terminalFailure = assertionFailure;
                    return terminalFailure;
                }

                EstablishAssertions(targetKey, relationship.TargetReference.Urn);

                if (relationship.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded)
                {
                    AddPendingAssertion(targetKey, path, relationship);
                    return null;
                }

                stateToAwait = known;
            }
            else
            {
                var assertionFailure = CheckOperationLocalAssertions(path, relationship, targetKey);
                if (assertionFailure is not null)
                {
                    terminalFailure = assertionFailure;
                    return terminalFailure;
                }

                EstablishAssertions(targetKey, relationship.TargetReference.Urn);

                if (relationship.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded)
                {
                    AddPendingAssertion(targetKey, path, relationship);
                    return null;
                }

                if (relationship.MaterializationAuthority != AIAssetGraphMaterializationAuthority.Required)
                {
                    throw new InvalidOperationException(
                        "Graph relationship materialization authority is outside the frozen schema-v1 vocabulary.");
                }

                var task = StartReferenceResolution(relationship.TargetReference, cancellationToken);
                stateToAwait = ResolutionState.InProgress(task, path, relationship);
                resolutions.Add(targetKey, stateToAwait);
            }
        }

        var resolutionResult = await stateToAwait.ResolutionTask!.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (terminalFailure is not null)
            {
                return terminalFailure;
            }

            var targetKey = AssetDefinitionKey.From(relationship.TargetReference);
            var current = resolutions[targetKey];
            if (current.Failure is not null)
            {
                terminalFailure = current.Failure;
                return terminalFailure;
            }

            if (current.Asset is not null)
            {
                var identityFailure = CheckKnownResolvedIdentity(path, relationship, current.Asset);
                if (identityFailure is not null)
                {
                    terminalFailure = identityFailure;
                }

                return terminalFailure;
            }

            var initiatingPath = current.InitiatingPath
                ?? throw new InvalidOperationException("An in-progress required resolution must retain its initiating path.");
            var initiatingRelationship = current.InitiatingRelationship
                ?? throw new InvalidOperationException("An in-progress required resolution must retain its initiating relationship.");

            switch (resolutionResult)
            {
                case AIAssetResolutionResult.Resolved resolved:
                {
                    EnsureResolvedIdentity(targetKey, resolved.Asset, "required reference");
                    if (!Equals(resolved.Asset.Urn, initiatingRelationship.TargetReference.Urn))
                    {
                        throw new InvalidOperationException(
                            "The persistent resolver returned a resolved Asset whose URN disagrees with the authored reference.");
                    }

                    var resolvedLineageFailure = CheckEstablishedLineage(
                        initiatingPath,
                        initiatingRelationship,
                        targetKey);
                    if (resolvedLineageFailure is not null)
                    {
                        terminalFailure = resolvedLineageFailure;
                        return terminalFailure;
                    }

                    CommitResolved(targetKey, resolved.Asset);

                    var pendingFailure = CheckPendingAssertions(targetKey, resolved.Asset);
                    if (pendingFailure is not null)
                    {
                        terminalFailure = pendingFailure;
                        return terminalFailure;
                    }

                    var currentRelationshipFailure = CheckKnownResolvedIdentity(path, relationship, resolved.Asset);
                    if (currentRelationshipFailure is not null)
                    {
                        terminalFailure = currentRelationshipFailure;
                    }

                    return terminalFailure;
                }

                case AIAssetResolutionResult.DefinitionNotPublished:
                {
                    var failure = new AIAssetGraphLoadResult.RequiredDefinitionUnavailable(
                        new AIAssetGraphRequiredDefinitionUnavailableContext(
                            rootKey,
                            initiatingPath,
                            initiatingRelationship));
                    CommitFailure(targetKey, failure);
                    return failure;
                }

                case AIAssetResolutionResult.ReferenceMismatch:
                {
                    var failure = new AIAssetGraphLoadResult.ReferenceIdentityConflict(
                        new AIAssetGraphReferenceIdentityConflictContext(
                            rootKey,
                            initiatingPath,
                            initiatingRelationship,
                            AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver));
                    CommitFailure(targetKey, failure);
                    return failure;
                }

                default:
                    throw UnexpectedResolverOutcome(resolutionResult, "exact-reference required resolution");
            }
        }
    }

    private Task<AIAssetResolutionResult> StartRootResolution(CancellationToken cancellationToken)
    {
        try
        {
            return resolver.ResolveAsync(rootKey, cancellationToken).AsTask();
        }
        catch (Exception exception)
        {
            return Task.FromException<AIAssetResolutionResult>(exception);
        }
    }

    private Task<AIAssetResolutionResult> StartReferenceResolution(
        AssetReference reference,
        CancellationToken cancellationToken)
    {
        try
        {
            return resolver.ResolveAsync(reference, cancellationToken).AsTask();
        }
        catch (Exception exception)
        {
            return Task.FromException<AIAssetResolutionResult>(exception);
        }
    }

    private void EnsureMaterializedSource(AssetDefinitionKey sourceKey)
    {
        if (!resolutions.TryGetValue(sourceKey, out var source) || source.Asset is null)
        {
            throw new InvalidOperationException(
                "A graph relationship may only originate from an operation-local successfully materialized source node.");
        }
    }

    private AIAssetGraphLoadResult? CheckOperationLocalAssertions(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship,
        AssetDefinitionKey targetKey)
    {
        if (assertedUrns.TryGetValue(targetKey, out var assertedUrn)
            && assertedUrn != relationship.TargetReference.Urn)
        {
            return new AIAssetGraphLoadResult.ReferenceIdentityConflict(
                new AIAssetGraphReferenceIdentityConflictContext(
                    rootKey,
                    path,
                    relationship,
                    AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity));
        }

        return CheckEstablishedLineage(path, relationship, targetKey);
    }

    private AIAssetGraphLoadResult? CheckEstablishedLineage(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship,
        AssetDefinitionKey targetKey)
    {
        if (!establishedLineages.TryGetValue(relationship.TargetReference.Urn, out var established)
            || SameLineageIdentity(established, targetKey))
        {
            return null;
        }

        return new AIAssetGraphLoadResult.LineageIdentityConflict(
            new AIAssetGraphLineageIdentityConflictContext(
                rootKey,
                path,
                relationship,
                relationship.TargetReference.Urn,
                established,
                targetKey));
    }

    private AIAssetGraphLoadResult? CheckKnownResolvedIdentity(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship,
        IAsset asset)
    {
        if (Equals(asset.Urn, relationship.TargetReference.Urn))
        {
            return null;
        }

        return new AIAssetGraphLoadResult.ReferenceIdentityConflict(
            new AIAssetGraphReferenceIdentityConflictContext(
                rootKey,
                path,
                relationship,
                AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity));
    }

    private AIAssetGraphLoadResult? CheckPendingAssertions(
        AssetDefinitionKey key,
        IAsset asset)
    {
        if (!pendingAssertions.TryGetValue(key, out var assertions))
        {
            return null;
        }

        pendingAssertions.Remove(key);

        foreach (var assertion in assertions)
        {
            if (!Equals(asset.Urn, assertion.Relationship.TargetReference.Urn))
            {
                return new AIAssetGraphLoadResult.ReferenceIdentityConflict(
                    new AIAssetGraphReferenceIdentityConflictContext(
                        rootKey,
                        assertion.Path,
                        assertion.Relationship,
                        AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity));
            }
        }

        return null;
    }

    private void EstablishAssertions(AssetDefinitionKey key, AssetUrn urn)
    {
        if (!assertedUrns.ContainsKey(key))
        {
            assertedUrns.Add(key, urn);
        }

        if (!establishedLineages.ContainsKey(urn))
        {
            establishedLineages.Add(urn, key);
        }
    }

    private void AddPendingAssertion(
        AssetDefinitionKey key,
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship)
    {
        if (!pendingAssertions.TryGetValue(key, out var assertions))
        {
            assertions = [];
            pendingAssertions.Add(key, assertions);
        }

        assertions.Add(new PendingAssertion(path, relationship));
    }

    private void CommitResolved(AssetDefinitionKey key, IAsset asset)
    {
        resolutions[key] = ResolutionState.Resolved(asset);
        if (nodes.All(node => node.DefinitionKey != key))
        {
            nodes.Add(new AIAssetGraphNode(key, asset));
        }

        EstablishAssertions(key, asset.Urn);
    }

    private void CommitFailure(
        AssetDefinitionKey key,
        AIAssetGraphLoadResult failure)
    {
        resolutions[key] = ResolutionState.Failed(failure);
        terminalFailure = failure;
    }

    private static void EnsureResolvedIdentity(
        AssetDefinitionKey requestedKey,
        IAsset asset,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (AssetDefinitionKey.From(asset) != requestedKey)
        {
            throw new InvalidOperationException(
                $"The persistent resolver returned an Asset for a different definition identity during {operation}.");
        }
    }

    private static void EnsurePathIdentifiesRelationship(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship)
    {
        if (path.Segments.Count == 0)
        {
            throw new ArgumentException(
                "A relationship resolution path must contain the supplied relationship occurrence.",
                nameof(path));
        }

        var final = path.Segments[^1].Relationship;
        if (final.SourceKey != relationship.SourceKey
            || final.TargetReference != relationship.TargetReference
            || final.RelationshipClass != relationship.RelationshipClass
            || final.MaterializationAuthority != relationship.MaterializationAuthority
            || final.BoundaryRole != relationship.BoundaryRole
            || final.DependencyRequired != relationship.DependencyRequired
            || final.LocalOrdinal != relationship.LocalOrdinal
            || !string.Equals(final.AuthoredPath, relationship.AuthoredPath, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The final path segment must identify the supplied relationship occurrence.",
                nameof(relationship));
        }
    }

    private static bool SameLineageIdentity(
        AssetDefinitionKey left,
        AssetDefinitionKey right) =>
        left.Type == right.Type && left.Id == right.Id;

    private static InvalidOperationException UnexpectedResolverOutcome(
        AIAssetResolutionResult result,
        string operation) =>
        new(
            $"Persistent resolver returned unsupported outcome '{result.GetType().Name}' during {operation}.");

    private sealed record PendingAssertion(
        AIAssetGraphPath Path,
        AIAssetGraphRelationship Relationship);

    private sealed record ResolutionState(
        Task<AIAssetResolutionResult>? ResolutionTask,
        AIAssetGraphPath? InitiatingPath,
        AIAssetGraphRelationship? InitiatingRelationship,
        IAsset? Asset,
        AIAssetGraphLoadResult? Failure)
    {
        internal static ResolutionState InProgress(
            Task<AIAssetResolutionResult> task,
            AIAssetGraphPath? path,
            AIAssetGraphRelationship? relationship) =>
            new(task, path, relationship, null, null);

        internal static ResolutionState Resolved(IAsset asset) =>
            new(null, null, null, asset, null);

        internal static ResolutionState Failed(AIAssetGraphLoadResult failure) =>
            new(null, null, null, null, failure);
    }
}
