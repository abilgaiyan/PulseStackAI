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
    private readonly IPersistentAIAssetResolver resolver;
    private readonly AssetDefinitionKey rootKey;
    private readonly Dictionary<AssetDefinitionKey, ResolutionState> resolutions = [];
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

    internal AIAssetGraphLoadResult? TerminalFailure => terminalFailure;

    internal IReadOnlyList<AIAssetGraphNode> MaterializedNodes =>
        new ReadOnlyCollection<AIAssetGraphNode>(nodes.ToArray());

    internal IReadOnlyList<AIAssetGraphRelationship> ObservedRelationships =>
        new ReadOnlyCollection<AIAssetGraphRelationship>(relationships.ToArray());

    internal async ValueTask<AIAssetGraphLoadResult?> ResolveRootAsync(
        CancellationToken cancellationToken = default)
    {
        if (terminalFailure is not null)
        {
            return terminalFailure;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (resolutions.TryGetValue(rootKey, out var known))
        {
            return known.Failure;
        }

        var result = await resolver.ResolveAsync(rootKey, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

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

    internal async ValueTask<AIAssetGraphLoadResult?> ProcessRelationshipAsync(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(relationship);

        if (terminalFailure is not null)
        {
            return terminalFailure;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (path.RootKey != rootKey)
        {
            throw new ArgumentException(
                "The relationship path must belong to this resolution operation's root key.",
                nameof(path));
        }

        EnsurePathIdentifiesRelationship(path, relationship);
        relationships.Add(relationship);

        var targetKey = AssetDefinitionKey.From(relationship.TargetReference);

        // Definition identity is the primary operation-local authority. Once the key is known,
        // same-key URN disagreement is AAG003 regardless of any other lineage evidence.
        if (resolutions.TryGetValue(targetKey, out var known))
        {
            if (known.Failure is not null)
            {
                terminalFailure = known.Failure;
                return terminalFailure;
            }

            var identityFailure = CheckKnownResolvedIdentity(
                path,
                relationship,
                known.Asset!);
            if (identityFailure is not null)
            {
                terminalFailure = identityFailure;
            }

            return terminalFailure;
        }

        var lineageFailure = CheckEstablishedLineage(path, relationship, targetKey);
        if (lineageFailure is not null)
        {
            terminalFailure = lineageFailure;
            return terminalFailure;
        }

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

        var result = await resolver.ResolveAsync(relationship.TargetReference, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        switch (result)
        {
            case AIAssetResolutionResult.Resolved resolved:
            {
                EnsureResolvedIdentity(targetKey, resolved.Asset, "required reference");

                if (!Equals(resolved.Asset.Urn, relationship.TargetReference.Urn))
                {
                    throw new InvalidOperationException(
                        "The persistent resolver returned a resolved Asset whose URN disagrees with the authored reference.");
                }

                var resolvedLineageFailure = CheckResolvedLineage(
                    path,
                    relationship,
                    targetKey,
                    resolved.Asset.Urn);
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
                }

                return terminalFailure;
            }

            case AIAssetResolutionResult.DefinitionNotPublished:
            {
                var failure = new AIAssetGraphLoadResult.RequiredDefinitionUnavailable(
                    new AIAssetGraphRequiredDefinitionUnavailableContext(
                        rootKey,
                        path,
                        relationship));
                CommitFailure(targetKey, failure);
                return failure;
            }

            case AIAssetResolutionResult.ReferenceMismatch:
            {
                var failure = new AIAssetGraphLoadResult.ReferenceIdentityConflict(
                    new AIAssetGraphReferenceIdentityConflictContext(
                        rootKey,
                        path,
                        relationship,
                        AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver));
                CommitFailure(targetKey, failure);
                return failure;
            }

            default:
                throw UnexpectedResolverOutcome(result, "exact-reference required resolution");
        }
    }

    private AIAssetGraphLoadResult? CheckEstablishedLineage(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship,
        AssetDefinitionKey targetKey)
    {
        if (!establishedLineages.TryGetValue(relationship.TargetReference.Urn, out var established))
        {
            return null;
        }

        if (SameLineageIdentity(established, targetKey))
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

    private AIAssetGraphLoadResult? CheckResolvedLineage(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship,
        AssetDefinitionKey targetKey,
        AssetUrn resolvedUrn)
    {
        if (!establishedLineages.TryGetValue(resolvedUrn, out var established)
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
            var lineageFailure = CheckEstablishedLineage(
                assertion.Path,
                assertion.Relationship,
                key);
            if (lineageFailure is not null)
            {
                return lineageFailure;
            }

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
        resolutions.Add(key, ResolutionState.Resolved(asset));
        nodes.Add(new AIAssetGraphNode(key, asset));

        if (!establishedLineages.TryGetValue(asset.Urn, out var established))
        {
            establishedLineages.Add(asset.Urn, key);
            return;
        }

        if (!SameLineageIdentity(established, key))
        {
            throw new InvalidOperationException(
                "A resolved Asset introduced a conflicting operation-local lineage identity without an authored relationship context.");
        }
    }

    private void CommitFailure(
        AssetDefinitionKey key,
        AIAssetGraphLoadResult failure)
    {
        resolutions.Add(key, ResolutionState.Failed(failure));
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
        IAsset? Asset,
        AIAssetGraphLoadResult? Failure)
    {
        internal static ResolutionState Resolved(IAsset asset) => new(asset, null);
        internal static ResolutionState Failed(AIAssetGraphLoadResult failure) => new(null, failure);
    }
}
