namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

internal static class AIAssetDocumentJsonProfile
{
    /// <summary>
    /// Maximum JSON nesting depth accepted or emitted by the schema-v1 codec.
    /// Reader and writer must use this same authority so canonical output is
    /// never rejected solely because of asymmetric framework defaults.
    /// </summary>
    internal const int MaxDepth = 256;
}
