namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Owns the canonical Library reference projection and Library membership invariants.
/// </summary>
internal static class LibraryReferenceProjection
{
    public static IReadOnlyCollection<AssetReference> Create(
        IReadOnlyCollection<AssetReference> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var snapshot = members.ToArray();
        var urnsByKey = new Dictionary<AssetDefinitionKey, string>();

        foreach (var reference in snapshot)
        {
            ArgumentNullException.ThrowIfNull(reference);
            EnsureAllowedMemberType(reference.Type);

            var key = AssetDefinitionKey.From(reference);

            if (urnsByKey.TryGetValue(key, out var existingUrn))
            {
                if (!string.Equals(existingUrn, reference.Urn.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Library member reference '{key}' has conflicting URNs.");
                }

                throw new InvalidOperationException(
                    $"Library member reference '{key}' is duplicated.");
            }

            urnsByKey.Add(key, reference.Urn.Value);
        }

        return snapshot;
    }

    private static void EnsureAllowedMemberType(AssetType assetType)
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
                $"Library cannot own an Asset of type '{assetType}'.");
        }
    }
}
