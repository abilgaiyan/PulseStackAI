using System.Text.Json;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

/// <summary>
/// Private deterministic and reversible path model for durable catalog records.
/// </summary>
internal static class AIAssetCatalogPathModel
{
    private const string RecordsDirectoryName = "records";
    private const string CatalogExtension = ".catalog";

    public static string GetRecordPath(string catalogRoot, AssetDefinitionKey key)
    {
        EnsureValidRoot(catalogRoot);
        AIAssetStorageContract.EnsureValidKey(key);
        AIAssetCatalogUnicode.EnsureWellFormed(key.Version.Value, nameof(key));

        return Path.Combine(
            catalogRoot,
            RecordsDirectoryName,
            AIAssetCatalogRecordCodec.ToAssetTypeToken(key.Type),
            key.Id.Value.ToString("N"),
            $"{EncodeVersion(key.Version)}{CatalogExtension}");
    }

    public static string EncodeVersion(AssetVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (string.IsNullOrWhiteSpace(version.Value))
        {
            throw new ArgumentException("Asset version must not be empty or whitespace.", nameof(version));
        }

        AIAssetCatalogUnicode.EnsureWellFormed(version.Value, nameof(version));

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

    public static AssetVersion DecodeVersion(string encodedVersion)
    {
        ArgumentNullException.ThrowIfNull(encodedVersion);
        if (encodedVersion.Length == 0 || encodedVersion.Length % 4 != 0)
        {
            throw new InvalidDataException("Encoded catalog version must contain complete four-hex-digit UTF-16 code units.");
        }

        var decoded = new char[encodedVersion.Length / 4];
        for (var index = 0; index < encodedVersion.Length; index += 4)
        {
            var value = 0;
            for (var digit = 0; digit < 4; digit++)
            {
                value = (value << 4) | ParseUpperHex(encodedVersion[index + digit]);
            }

            decoded[index / 4] = (char)value;
        }

        var versionValue = new string(decoded);
        try
        {
            AIAssetCatalogUnicode.EnsureWellFormed(versionValue, nameof(encodedVersion));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("Encoded catalog version does not represent well-formed Unicode.", ex);
        }

        var version = new AssetVersion(versionValue);
        if (string.IsNullOrWhiteSpace(version.Value))
        {
            throw new InvalidDataException("Encoded catalog version resolves to an empty or whitespace version.");
        }

        if (!string.Equals(EncodeVersion(version), encodedVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Encoded catalog version is not canonical.");
        }

        return version;
    }

    public static AssetDefinitionKey ParseRecordPath(string catalogRoot, string recordPath)
    {
        EnsureValidRoot(catalogRoot);
        if (string.IsNullOrWhiteSpace(recordPath))
        {
            throw new ArgumentException("Catalog record path must not be empty or whitespace.", nameof(recordPath));
        }

        var relative = Path.GetRelativePath(catalogRoot, recordPath);
        var segments = relative.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 4
            || !string.Equals(segments[0], RecordsDirectoryName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Catalog record path does not match the canonical record topology.");
        }

        AssetType type;
        try
        {
            type = AIAssetCatalogRecordCodec.FromAssetTypeToken(segments[1]);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Catalog record path contains an invalid asset type token.", ex);
        }

        if (!Guid.TryParseExact(segments[2], "N", out var idValue)
            || !string.Equals(segments[2], idValue.ToString("N"), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Catalog record path asset ID is not canonical lowercase Guid N format.");
        }

        var fileName = segments[3];
        if (!fileName.EndsWith(CatalogExtension, StringComparison.Ordinal)
            || fileName.Length == CatalogExtension.Length)
        {
            throw new InvalidDataException("Catalog record filename must use the exact canonical .catalog extension.");
        }

        var encodedVersion = fileName[..^CatalogExtension.Length];
        var version = DecodeVersion(encodedVersion);
        var key = new AssetDefinitionKey(type, new AssetId(idValue), version);
        AIAssetStorageContract.EnsureValidKey(key);

        var expected = GetRecordPath(catalogRoot, key);
        if (!string.Equals(recordPath, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Catalog record path is not in exact canonical form.");
        }

        return key;
    }

    public static void ValidateRecordPath(string catalogRoot, string recordPath, CatalogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var pathKey = ParseRecordPath(catalogRoot, recordPath);

        if (pathKey != record.DefinitionKey)
        {
            throw new InvalidDataException("Catalog record path identity does not match the decoded record identity.");
        }
    }

    private static int ParseUpperHex(char value) => value switch
    {
        >= '0' and <= '9' => value - '0',
        >= 'A' and <= 'F' => value - 'A' + 10,
        _ => throw new InvalidDataException("Encoded catalog version must use uppercase hexadecimal digits only.")
    };

    private static char ToHex(int value) => value < 10
        ? (char)('0' + value)
        : (char)('A' + value - 10);

    private static void EnsureValidRoot(string catalogRoot)
    {
        if (string.IsNullOrWhiteSpace(catalogRoot))
        {
            throw new ArgumentException("Catalog root must not be empty or whitespace.", nameof(catalogRoot));
        }
    }
}
