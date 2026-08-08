using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Runtime.Realization.Resolution;

/// <summary>
/// Resolves assets from an in-memory immutable catalog.
/// </summary>
public sealed class InMemoryAssetResolver : IAssetResolver
{
    private readonly IReadOnlyDictionary<AssetId, IAsset> _assets;

    public InMemoryAssetResolver(IEnumerable<IAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var catalog = new Dictionary<AssetId, IAsset>();

        foreach (var asset in assets)
        {
            ArgumentNullException.ThrowIfNull(asset);
            asset.Id.EnsureValid();

            if (!catalog.TryAdd(asset.Id, asset))
            {
                throw new ArgumentException(
                    $"An asset with id '{asset.Id}' is already registered.",
                    nameof(assets));
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

        if (reference.Id.IsEmpty || string.IsNullOrWhiteSpace(reference.Urn.Value))
        {
            return ValueTask.FromResult<IAsset?>(null);
        }

        if (!_assets.TryGetValue(reference.Id, out var asset))
        {
            return ValueTask.FromResult<IAsset?>(null);
        }

        return ValueTask.FromResult<IAsset?>(
            string.Equals(asset.Urn.Value, reference.Urn.Value, StringComparison.Ordinal)
                ? asset
                : null);
    }
}
