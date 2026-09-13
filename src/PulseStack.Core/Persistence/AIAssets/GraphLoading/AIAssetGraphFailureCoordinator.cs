using System.Runtime.ExceptionServices;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// Invocation-local B.5 authority for selecting and committing one graph-semantic failure.
/// Candidate observation order and completion timing are deliberately non-authoritative.
/// </summary>
internal sealed class AIAssetGraphFailureCoordinator
{
    private readonly object gate = new();
    private readonly AssetDefinitionKey rootKey;
    private AIAssetGraphLoadResult? bestCandidate;
    private AIAssetGraphLoadResult? committedFailure;

    internal AIAssetGraphFailureCoordinator(AssetDefinitionKey rootKey)
    {
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        this.rootKey = rootKey;
    }

    internal AssetDefinitionKey RootKey => rootKey;

    internal AIAssetGraphLoadResult? CommittedFailure
    {
        get
        {
            lock (gate)
            {
                return committedFailure;
            }
        }
    }

    internal void Observe(AIAssetGraphLoadResult failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        EnsureSemanticFailure(failure);
        EnsureMatchingRoot(failure);

        lock (gate)
        {
            if (committedFailure is not null)
            {
                return;
            }

            if (bestCandidate is null
                || AIAssetGraphFailureComparer.Compare(failure, bestCandidate) < 0)
            {
                bestCandidate = failure;
            }
        }
    }

    /// <summary>
    /// Commits the best candidate only after the caller has established that no eligible
    /// observation capable of superseding it remains outstanding.
    /// </summary>
    internal AIAssetGraphLoadResult? Commit(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (committedFailure is not null)
            {
                return committedFailure;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (bestCandidate is null)
            {
                return null;
            }

            committedFailure = bestCandidate;
            return committedFailure;
        }
    }

    /// <summary>
    /// Preserves predecessor exception identity unless a graph-semantic failure was already
    /// terminally committed. Caller cancellation observed before commitment has authority.
    /// </summary>
    internal AIAssetGraphLoadResult? PreservePredecessorFailure(
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (gate)
        {
            if (committedFailure is not null)
            {
                return committedFailure;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
        throw new InvalidOperationException("Unreachable predecessor failure continuation.");
    }

    private void EnsureMatchingRoot(AIAssetGraphLoadResult result)
    {
        var observedRoot = result switch
        {
            AIAssetGraphLoadResult.RootDefinitionUnavailable value => value.Context.RootKey,
            AIAssetGraphLoadResult.RequiredDefinitionUnavailable value => value.Context.RootKey,
            AIAssetGraphLoadResult.ReferenceIdentityConflict value => value.Context.RootKey,
            AIAssetGraphLoadResult.LineageIdentityConflict value => value.Context.RootKey,
            AIAssetGraphLoadResult.RequiredMaterializationCycle value => value.Context.RootKey,
            _ => throw new ArgumentException("A graph success result has no failure context.", nameof(result))
        };

        if (observedRoot != rootKey)
        {
            throw new ArgumentException(
                "The observed graph-semantic failure must belong to this coordinator's exact graph-load RootKey.",
                nameof(result));
        }
    }

    private static void EnsureSemanticFailure(AIAssetGraphLoadResult result)
    {
        if (result is AIAssetGraphLoadResult.Success)
        {
            throw new ArgumentException(
                "The B.5 failure coordinator accepts graph-semantic failures only.",
                nameof(result));
        }

        if (result is not (
            AIAssetGraphLoadResult.RootDefinitionUnavailable
            or AIAssetGraphLoadResult.RequiredDefinitionUnavailable
            or AIAssetGraphLoadResult.ReferenceIdentityConflict
            or AIAssetGraphLoadResult.LineageIdentityConflict
            or AIAssetGraphLoadResult.RequiredMaterializationCycle))
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }
    }
}

/// <summary>
/// Frozen B.5 canonical structural-path and same-path failure precedence authority.
/// </summary>
internal static class AIAssetGraphFailureComparer
{
    private static readonly IReadOnlyDictionary<AssetType, int> TypeRanks =
        new Dictionary<AssetType, int>
        {
            [AssetType.Project] = 0,
            [AssetType.Library] = 1,
            [AssetType.Package] = 2,
            [AssetType.Workflow] = 3,
            [AssetType.Agent] = 4,
            [AssetType.Prompt] = 5,
            [AssetType.Tool] = 6,
            [AssetType.Knowledge] = 7,
            [AssetType.Memory] = 8,
            [AssetType.Policy] = 9,
            [AssetType.Model] = 10
        };

    internal static int Compare(AIAssetGraphLoadResult left, AIAssetGraphLoadResult right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftContext = GetContext(left);
        var rightContext = GetContext(right);
        var pathComparison = ComparePaths(leftContext.CanonicalPath, rightContext.CanonicalPath);
        if (pathComparison != 0)
        {
            return pathComparison;
        }

        return Precedence(left).CompareTo(Precedence(right));
    }

    internal static int ComparePaths(AIAssetGraphPath left, AIAssetGraphPath right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var rootComparison = CompareDefinitionKeys(left.RootKey, right.RootKey);
        if (rootComparison != 0)
        {
            return rootComparison;
        }

        var sharedLength = Math.Min(left.Segments.Count, right.Segments.Count);
        for (var index = 0; index < sharedLength; index++)
        {
            var segmentComparison = CompareRelationships(
                left.Segments[index].Relationship,
                right.Segments[index].Relationship);
            if (segmentComparison != 0)
            {
                return segmentComparison;
            }
        }

        return left.Segments.Count.CompareTo(right.Segments.Count);
    }

    internal static int CompareRelationships(
        AIAssetGraphRelationship left,
        AIAssetGraphRelationship right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var ordinalComparison = left.LocalOrdinal.CompareTo(right.LocalOrdinal);
        if (ordinalComparison != 0)
        {
            return ordinalComparison;
        }

        var sourceComparison = CompareDefinitionKeys(left.SourceKey, right.SourceKey);
        if (sourceComparison != 0)
        {
            return sourceComparison;
        }

        var targetComparison = CompareDefinitionKeys(
            AssetDefinitionKey.From(left.TargetReference),
            AssetDefinitionKey.From(right.TargetReference));
        if (targetComparison != 0)
        {
            return targetComparison;
        }

        return string.CompareOrdinal(left.AuthoredPath, right.AuthoredPath);
    }

    private static int CompareDefinitionKeys(AssetDefinitionKey left, AssetDefinitionKey right)
    {
        var typeComparison = TypeRanks[left.Type].CompareTo(TypeRanks[right.Type]);
        if (typeComparison != 0)
        {
            return typeComparison;
        }

        var idComparison = string.CompareOrdinal(
            left.Id.Value.ToString("D").ToLowerInvariant(),
            right.Id.Value.ToString("D").ToLowerInvariant());
        if (idComparison != 0)
        {
            return idComparison;
        }

        return string.CompareOrdinal(left.Version.Value, right.Version.Value);
    }

    private static int Precedence(AIAssetGraphLoadResult result) => result switch
    {
        AIAssetGraphLoadResult.ReferenceIdentityConflict => 0,
        AIAssetGraphLoadResult.LineageIdentityConflict => 0,
        AIAssetGraphLoadResult.RequiredDefinitionUnavailable => 1,
        AIAssetGraphLoadResult.RequiredMaterializationCycle => 2,
        AIAssetGraphLoadResult.RootDefinitionUnavailable => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };

    private static AIAssetGraphFailureContext GetContext(AIAssetGraphLoadResult result) => result switch
    {
        AIAssetGraphLoadResult.RootDefinitionUnavailable value => value.Context,
        AIAssetGraphLoadResult.RequiredDefinitionUnavailable value => value.Context,
        AIAssetGraphLoadResult.ReferenceIdentityConflict value => value.Context,
        AIAssetGraphLoadResult.LineageIdentityConflict value => value.Context,
        AIAssetGraphLoadResult.RequiredMaterializationCycle value => value.Context,
        _ => throw new ArgumentException("A graph success result has no failure context.", nameof(result))
    };
}
