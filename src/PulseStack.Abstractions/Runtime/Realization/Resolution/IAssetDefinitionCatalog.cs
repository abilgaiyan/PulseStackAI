using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Resolution;

/// <summary>
/// Provides diagnostic lookup of declarative Assets by authoritative definition identity.
/// </summary>
public interface IAssetDefinitionCatalog
{
    /// <summary>
    /// Finds the Asset registered for an exact definition key.
    /// </summary>
    /// <param name="key">The authoritative definition identity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered Asset, or <see langword="null"/> when no definition is available.</returns>
    ValueTask<IAsset?> FindAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default);
}
