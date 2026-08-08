using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Resolution;

/// <summary>
/// Resolves a declarative asset reference into the asset used during runtime realization.
/// </summary>
public interface IAssetResolver
{
    /// <summary>
    /// Resolves an asset reference.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved asset, or <see langword="null"/> when the reference cannot be resolved.</returns>
    ValueTask<IAsset?> ResolveAsync(
        AssetReference reference,
        CancellationToken cancellationToken = default);
}
