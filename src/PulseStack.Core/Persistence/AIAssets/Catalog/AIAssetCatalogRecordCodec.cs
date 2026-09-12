using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

/// <summary>
/// Private canonical codec for durable catalog records. This is intentionally not an
/// AIAssetDocument codec and has no public persistence contract of its own.
/// </summary>
internal static class AIAssetCatalogRecordCodec
{
    internal const int MaximumRecordBytes = 1024 * 1024;
    private const string FormatVersion = "1.0";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
        SkipValidation = false
    };

    public static byte[] Serialize(CatalogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        EnsureWellFormedUnicode(record.DefinitionKey.Version.Value, nameof(record));
        EnsureWellFormedUnicode(record.Urn.Value, nameof(record));

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("formatVersion", FormatVersion);
            writer.WriteString("assetType", ToAssetTypeToken(record.DefinitionKey.Type));
            writer.WriteString("assetId", record.DefinitionKey.Id.Value.ToString("N"));
            writer.WriteString("version", record.DefinitionKey.Version.Value);
            writer.WriteString("urn", record.Urn.Value);
            writer.WriteEndObject();
            writer.Flush();
        }

        var bytes = buffer.WrittenSpan.ToArray();
        if (bytes.Length > MaximumRecordBytes)
        {
            throw new InvalidDataException($"Catalog record exceeds the {MaximumRecordBytes}-byte limit.");
        }

        return bytes;
    }

    public static CatalogRecord Deserialize(ReadOnlySpan<byte> representation)
    {
        if (representation.Length == 0)
        {
            throw new InvalidDataException("Catalog record is empty.");
        }

        if (representation.Length > MaximumRecordBytes)
        {
            throw new InvalidDataException($"Catalog record exceeds the {MaximumRecordBytes}-byte limit.");
        }

        if (representation.Length >= 3
            && representation[0] == 0xEF
            && representation[1] == 0xBB
            && representation[2] == 0xBF)
        {
            throw new InvalidDataException("Catalog record must not contain a UTF-8 BOM.");
        }

        try
        {
            _ = StrictUtf8.GetString(representation);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("Catalog record is not valid UTF-8.", ex);
        }

        CatalogRecord record;
        try
        {
            record = ReadRecord(representation);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Catalog record is not valid canonical JSON.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("Catalog record contains an invalid identity value.", ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Catalog record contains an invalid identity value.", ex);
        }

        var canonical = Serialize(record);
        if (!representation.SequenceEqual(canonical))
        {
            throw new InvalidDataException("Catalog record is not in canonical byte form.");
        }

        return record;
    }

    private static CatalogRecord ReadRecord(ReadOnlySpan<byte> representation)
    {
        var reader = new Utf8JsonReader(
            representation,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 2
            });

        RequireRead(ref reader, JsonTokenType.StartObject);

        string? formatVersion = null;
        string? assetType = null;
        string? assetId = null;
        string? version = null;
        string? urn = null;
        var members = new HashSet<string>(StringComparer.Ordinal);

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Catalog record members must be JSON properties.");
            }

            var propertyName = reader.GetString() ?? throw new JsonException("Catalog record property name is null.");
            if (!members.Add(propertyName))
            {
                throw new JsonException($"Duplicate catalog record member '{propertyName}'.");
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Catalog record member '{propertyName}' must be a string.");
            }

            var value = reader.GetString() ?? throw new JsonException($"Catalog record member '{propertyName}' is null.");
            EnsureWellFormedUnicode(value, propertyName);

            switch (propertyName)
            {
                case "formatVersion":
                    formatVersion = value;
                    break;
                case "assetType":
                    assetType = value;
                    break;
                case "assetId":
                    assetId = value;
                    break;
                case "version":
                    version = value;
                    break;
                case "urn":
                    urn = value;
                    break;
                default:
                    throw new JsonException($"Unknown catalog record member '{propertyName}'.");
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject || reader.Read())
        {
            throw new JsonException("Catalog record must contain exactly one JSON object.");
        }

        if (members.Count != 5
            || formatVersion is null
            || assetType is null
            || assetId is null
            || version is null
            || urn is null)
        {
            throw new JsonException("Catalog record is missing one or more required members.");
        }

        if (!string.Equals(formatVersion, FormatVersion, StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported catalog record formatVersion '{formatVersion}'.");
        }

        if (!Guid.TryParseExact(assetId, "N", out var idValue))
        {
            throw new JsonException("Catalog record assetId must use lowercase Guid N format.");
        }

        var canonicalId = idValue.ToString("N");
        if (!string.Equals(assetId, canonicalId, StringComparison.Ordinal))
        {
            throw new JsonException("Catalog record assetId is not canonical lowercase Guid N format.");
        }

        var key = new AssetDefinitionKey(
            FromAssetTypeToken(assetType),
            new AssetId(idValue),
            new AssetVersion(version));

        return new CatalogRecord(key, new AssetUrn(urn));
    }

    private static void RequireRead(ref Utf8JsonReader reader, JsonTokenType expected)
    {
        if (!reader.Read() || reader.TokenType != expected)
        {
            throw new JsonException($"Expected JSON token '{expected}'.");
        }
    }

    internal static string ToAssetTypeToken(AssetType type) => type switch
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

    private static AssetType FromAssetTypeToken(string token) => token switch
    {
        "project" => AssetType.Project,
        "library" => AssetType.Library,
        "package" => AssetType.Package,
        "workflow" => AssetType.Workflow,
        "agent" => AssetType.Agent,
        "prompt" => AssetType.Prompt,
        "tool" => AssetType.Tool,
        "knowledge" => AssetType.Knowledge,
        "memory" => AssetType.Memory,
        "policy" => AssetType.Policy,
        "model" => AssetType.Model,
        "provider" => throw new JsonException("The reserved provider asset type cannot be persisted in the catalog."),
        _ => throw new JsonException($"Unknown catalog assetType token '{token}'.")
    };

    private static void EnsureWellFormedUnicode(string value, string parameterName)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (!char.IsSurrogate(current))
            {
                continue;
            }

            if (!char.IsHighSurrogate(current)
                || index + 1 >= value.Length
                || !char.IsLowSurrogate(value[index + 1]))
            {
                throw new ArgumentException("Catalog record strings must contain well-formed Unicode.", parameterName);
            }

            index++;
        }
    }
}
