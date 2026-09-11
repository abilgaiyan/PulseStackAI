using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Represents the semantic result of loading one exact AI Asset definition.
/// </summary>
public abstract record AIAssetLoadResult
{
    private AIAssetLoadResult()
    {
    }

    public sealed record Loaded : AIAssetLoadResult
    {
        public Loaded(IAsset asset)
        {
            ArgumentNullException.ThrowIfNull(asset);
            Asset = asset;
        }

        public IAsset Asset { get; }
    }

    public sealed record NotFound : AIAssetLoadResult;
}
