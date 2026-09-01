using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Resolution;

namespace PulseStack.Core.Runtime.Realization.Resolution;

/// <summary>
/// Resolves assets from an in-memory immutable catalog.
/// </summary>
public sealed class InMemoryAssetResolver : IAssetResolver
{
    private readonly IReadOnlyDictionary<AssetDefinitionKey, IAsset> _assets;

    public InMemoryAssetResolver(IEnumerable<IAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var catalog = new Dictionary<AssetDefinitionKey, IAsset>();

        foreach (var asset in assets)
        {
            ArgumentNullException.ThrowIfNull(asset);
            asset.Id.EnsureValid();

            var key = AssetDefinitionKey.From(asset);
            if (!catalog.TryAdd(key, asset))
            {
                var registered = catalog[key];
                var message = string.Equals(
                    registered.Urn.Value,
                    asset.Urn.Value,
                    StringComparison.Ordinal)
                    ? $"Asset definition '{asset.Type}/{asset.Id}/{asset.Version.Value}' is already registered."
                    : $"Asset definition '{asset.Type}/{asset.Id}/{asset.Version.Value}' is already registered with URN '{registered.Urn.Value}' and cannot also use URN '{asset.Urn.Value}'.";

                throw new ArgumentException(message, nameof(assets));
            }
        }

        _assets = catalog;
    }

    public ValueTask<IAsset?> ResolveAsync(
        AssetReference reference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(reference);

        if (!Enum.IsDefined(reference.Type)
            || reference.Id.IsEmpty
            || string.IsNullOrWhiteSpace(reference.Urn.Value)
            || string.IsNullOrWhiteSpace(reference.Version.Value))
        {
            return ValueTask.FromResult<IAsset?>(null);
        }

        if (!_assets.TryGetValue(AssetDefinitionKey.From(reference), out var asset))
        {
            return ValueTask.FromResult<IAsset?>(null);
        }

        return ValueTask.FromResult<IAsset?>(
            string.Equals(
                asset.Urn.Value,
                reference.Urn.Value,
                StringComparison.Ordinal)
                ? asset
                : null);
    }
}
