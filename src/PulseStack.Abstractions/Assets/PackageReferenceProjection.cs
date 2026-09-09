namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Owns the canonical Package reference projection and direct membership invariants.
/// </summary>
internal static class PackageReferenceProjection
{
    public static IReadOnlyList<AssetReference> Create(
        IReadOnlyList<AssetReference> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var snapshot = members.ToArray();

        if (snapshot.Length == 0)
        {
            throw new InvalidOperationException(
                "Package must declare at least one distribution member.");
        }

        var urnsByKey = new Dictionary<AssetDefinitionKey, string>();

        foreach (var reference in snapshot)
        {
            ArgumentNullException.ThrowIfNull(reference);

            if (!Enum.IsDefined(reference.Type))
            {
                throw new InvalidOperationException(
                    $"Package cannot include an unknown Asset type '{reference.Type}'.");
            }

            var key = AssetDefinitionKey.From(reference);

            if (urnsByKey.TryGetValue(key, out var existingUrn))
            {
                if (!string.Equals(existingUrn, reference.Urn.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Package member reference '{key}' has conflicting URNs.");
                }

                throw new InvalidOperationException(
                    $"Package member reference '{key}' is duplicated.");
            }

            urnsByKey.Add(key, reference.Urn.Value);
        }

        return snapshot;
    }
}
