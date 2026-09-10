using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Shared provider-neutral validation for MS-009.7 public contract values.
/// </summary>
public static class AIAssetStorageContract
{
    public static void EnsureValidKey(AssetDefinitionKey key)
    {
        if (!Enum.IsDefined(key.Type) || key.Type == AssetType.Provider)
        {
            throw new ArgumentException(
                "The asset definition key must identify a supported schema-v1 AI Asset type.",
                nameof(key));
        }

        if (key.Id.IsEmpty)
        {
            throw new ArgumentException(
                "The asset definition key must contain a non-empty AssetId.",
                nameof(key));
        }

        if (key.Version is null || string.IsNullOrWhiteSpace(key.Version.Value))
        {
            throw new ArgumentException(
                "The asset definition key must contain a non-empty AssetVersion.",
                nameof(key));
        }
    }

    public static void EnsureValidOptions(AIAssetStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumRepresentationSizeBytes <= 0)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.Configuration,
                "MaximumRepresentationSizeBytes must be greater than zero.");
        }
    }
}
