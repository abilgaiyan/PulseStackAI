namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Owns the canonical Package reference projection and direct membership invariants.
/// </summary>
internal static class PackageReferenceProjection
{
    public static IReadOnlyList<AssetReference> Create(
        AssetDefinitionKey packageKey,
        IReadOnlyList<AssetReference> members,
        IReadOnlyList<AssetDependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(dependencies);

        var snapshot = members.ToArray();

        if (snapshot.Length == 0)
        {
            throw new InvalidOperationException(
                "Package must declare at least one distribution member.");
        }

        var membersByKey = new Dictionary<AssetDefinitionKey, AssetReference>();

        foreach (var reference in snapshot)
        {
            ArgumentNullException.ThrowIfNull(reference);

            if (!Enum.IsDefined(reference.Type))
            {
                throw new InvalidOperationException(
                    $"Package cannot include an unknown Asset type '{reference.Type}'.");
            }

            var key = AssetDefinitionKey.From(reference);

            if (key == packageKey)
            {
                throw new InvalidOperationException(
                    "Package cannot include itself as a direct member.");
            }

            if (membersByKey.TryGetValue(key, out var existing))
            {
                if (!string.Equals(
                        existing.Urn.Value,
                        reference.Urn.Value,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Package member reference '{key}' has conflicting URNs.");
                }

                throw new InvalidOperationException(
                    $"Package member reference '{key}' is duplicated.");
            }

            membersByKey.Add(key, reference);
        }

        ValidateDependencies(packageKey, membersByKey, dependencies);

        return snapshot;
    }

    private static void ValidateDependencies(
        AssetDefinitionKey packageKey,
        IReadOnlyDictionary<AssetDefinitionKey, AssetReference> membersByKey,
        IReadOnlyList<AssetDependency> dependencies)
    {
        var dependenciesByKey = new Dictionary<AssetDefinitionKey, AssetDependency>();

        foreach (var dependency in dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            ArgumentNullException.ThrowIfNull(dependency.Reference);

            var key = AssetDefinitionKey.From(dependency.Reference);

            if (key == packageKey)
            {
                throw new InvalidOperationException(
                    "Package cannot depend on itself.");
            }

            if (dependenciesByKey.TryGetValue(key, out var existingDependency))
            {
                if (!string.Equals(
                        existingDependency.Reference.Urn.Value,
                        dependency.Reference.Urn.Value,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Package dependency reference '{key}' has conflicting URNs.");
                }

                if (existingDependency.Required != dependency.Required)
                {
                    throw new InvalidOperationException(
                        $"Package dependency reference '{key}' has conflicting Required values.");
                }

                throw new InvalidOperationException(
                    $"Package dependency reference '{key}' is duplicated.");
            }

            if (membersByKey.TryGetValue(key, out var member))
            {
                if (!string.Equals(
                        member.Urn.Value,
                        dependency.Reference.Urn.Value,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Package member and dependency reference '{key}' have conflicting URNs.");
                }

                throw new InvalidOperationException(
                    "Package members cannot also be declared as external dependencies.");
            }

            dependenciesByKey.Add(key, dependency);
        }
    }
}
