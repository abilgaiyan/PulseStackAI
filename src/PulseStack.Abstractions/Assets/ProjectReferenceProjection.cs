namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Owns the canonical Project reference projection and Project ownership invariants.
/// </summary>
internal static class ProjectReferenceProjection
{
    public static IReadOnlyCollection<AssetReference> Create(
        AssetReference entryWorkflow,
        IReadOnlyCollection<AssetReference> ownedAssets)
    {
        ArgumentNullException.ThrowIfNull(entryWorkflow);
        ArgumentNullException.ThrowIfNull(ownedAssets);

        if (entryWorkflow.Type != AssetType.Workflow)
        {
            throw new InvalidOperationException(
                "Project entry workflow must reference a Workflow Asset.");
        }

        var owned = ownedAssets.ToArray();
        var urnsByKey = new Dictionary<AssetDefinitionKey, string>();
        var entryKey = AssetDefinitionKey.From(entryWorkflow);
        var entryOwnedExactly = false;

        foreach (var reference in owned)
        {
            ArgumentNullException.ThrowIfNull(reference);
            EnsureAllowedOwnedType(reference.Type);

            var key = AssetDefinitionKey.From(reference);

            if (urnsByKey.TryGetValue(key, out var existingUrn))
            {
                if (!string.Equals(existingUrn, reference.Urn.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Project owned reference '{key}' has conflicting URNs.");
                }

                throw new InvalidOperationException(
                    $"Project owned reference '{key}' is duplicated.");
            }

            urnsByKey.Add(key, reference.Urn.Value);

            if (key == entryKey
                && string.Equals(reference.Urn.Value, entryWorkflow.Urn.Value, StringComparison.Ordinal))
            {
                entryOwnedExactly = true;
            }
        }

        if (!entryOwnedExactly)
        {
            throw new InvalidOperationException(
                "Project entry workflow must be exactly included in OwnedAssets.");
        }

        var projection = new List<AssetReference> { entryWorkflow };

        foreach (var reference in owned)
        {
            if (AssetDefinitionKey.From(reference) != entryKey)
            {
                projection.Add(reference);
            }
        }

        return projection.ToArray();
    }

    private static void EnsureAllowedOwnedType(AssetType assetType)
    {
        if (assetType is not AssetType.Workflow
            and not AssetType.Agent
            and not AssetType.Prompt
            and not AssetType.Tool
            and not AssetType.Knowledge
            and not AssetType.Memory
            and not AssetType.Policy
            and not AssetType.Model)
        {
            throw new InvalidOperationException(
                $"Project cannot own an Asset of type '{assetType}'.");
        }
    }
}
