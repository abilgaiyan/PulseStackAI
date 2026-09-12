using System.Globalization;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

/// <summary>
/// Private deterministic path model for durable catalog records.
/// </summary>
internal static class AIAssetCatalogPathModel
{
    public static string GetRecordPath(string catalogRoot, AssetDefinitionKey key)
    {
        if (string.IsNullOrWhiteSpace(catalogRoot))
        {
            throw new ArgumentException("Catalog root must not be empty or whitespace.", nameof(catalogRoot));
        }

        AIAssetStorageContract.EnsureValidKey(key);

        return Path.Combine(
            catalogRoot,
            "records",
            ToAssetTypeToken(key.Type),
            key.Id.Value.ToString("N"),
            $"{EncodeVersion(key.Version)}.catalog");
    }

    public static string EncodeVersion(AssetVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (string.IsNullOrWhiteSpace(version.Value))
        {
            throw new ArgumentException("Asset version must not be empty or whitespace.", nameof(version));
        }

        var encoded = new char[version.Value.Length * 4];
        var offset = 0;
        foreach (var codeUnit in version.Value)
        {
            var value = (ushort)codeUnit;
            encoded[offset++] = ToHex((value >> 12) & 0xF);
            encoded[offset++] = ToHex((value >> 8) & 0xF);
            encoded[offset++] = ToHex((value >> 4) & 0xF);
            encoded[offset++] = ToHex(value & 0xF);
        }

        return new string(encoded);
    }

    private static char ToHex(int value) => value < 10
        ? (char)('0' + value)
        : (char)('A' + value - 10);

    private static string ToAssetTypeToken(AssetType type) => type switch
    {
        AssetType.Project => "project",
        AssetType.Library => "library",
        AssetType.Package => "package",
        AssetType.Workflow => "workflow",
        AssetType.Agent => "agent",
        AssetType.Prompt => "prompt",
        AssetType.Tool => "tool",
        AssetType.Knowledge => "knowledge",
        AssetType.Memory => "memory",
        AssetType.Policy => "policy",
        AssetType.Model => "model",
        AssetType.Provider => throw new ArgumentException("Provider is not a persistent catalog asset type.", nameof(type)),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown AI Asset type.")
    };
}
