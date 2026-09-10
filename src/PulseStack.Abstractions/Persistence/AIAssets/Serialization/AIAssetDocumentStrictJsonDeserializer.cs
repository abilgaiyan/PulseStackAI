using System.Text;
using System.Text.Json;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

internal static class AIAssetDocumentStrictJsonDeserializer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly HashSet<string> SharedRootMembers = new(StringComparer.Ordinal)
    {
        "assetType", "dependencies", "identity", "lifecycle", "metadata", "references", "schemaVersion"
    };

    private static readonly IReadOnlyDictionary<string, string[]> RootSpecificMembers =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["project"] = ["entryWorkflow", "ownedAssets"],
            ["library"] = ["members"],
            ["package"] = ["members"],
            ["workflow"] = ["steps"],
            ["agent"] = ["goal", "knowledge", "memory", "model", "policies", "prompt", "responsibilities", "role", "tools"],
            ["prompt"] = ["systemInstructions"],
            ["tool"] = [],
            ["knowledge"] = [],
            ["memory"] = [],
            ["policy"] = [],
            ["model"] = ["model", "provider"]
        };

    internal static ParsedRoot Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        EnsureValidUtf16(json);
        return ParseUtf8(StrictUtf8.GetBytes(json), allowLeadingBom: false);
    }

    internal static ParsedRoot Parse(ReadOnlyMemory<byte> utf8Json)
    {
        var span = utf8Json.Span;
        try
        {
            _ = StrictUtf8.GetCharCount(span);
        }
        catch (DecoderFallbackException exception)
        {
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidEncoding,
                "Input contains malformed UTF-8.", inner: exception);
        }

        return ParseUtf8(span, allowLeadingBom: true);
    }

    private static ParsedRoot ParseUtf8(ReadOnlySpan<byte> utf8Json, bool allowLeadingBom)
    {
        if (allowLeadingBom && utf8Json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB && utf8Json[2] == 0xBF)
        {
            utf8Json = utf8Json[3..];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = AIAssetDocumentJsonProfile.MaxDepth
            });
        }
        catch (JsonException exception)
        {
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidJson,
                "Input is not one complete JSON artifact.", inner: exception);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind,
                    "The AI Asset document root must be a JSON object.");
            }

            var root = document.RootElement;
            RejectDuplicateMembers(root);

            var assetType = RequireString(root, "assetType");
            var descriptor = AIAssetDocumentRootDiscriminator.ResolveForDeserialization(assetType);

            var schemaVersion = RequireSchemaVersion(root);
            ValidateRootMembers(root, descriptor.Token);

            return new ParsedRoot(root.Clone(), descriptor, schemaVersion);
        }
    }

    private static void ValidateRootMembers(JsonElement root, string assetType)
    {
        var allowed = new HashSet<string>(SharedRootMembers, StringComparer.Ordinal);
        foreach (var member in RootSpecificMembers[assetType]) allowed.Add(member);

        foreach (var property in root.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw Codec(AIAssetDocumentCodecFailureReason.UnknownMember,
                    $"Unknown root member '{property.Name}'.", property.Name);
            }
        }
    }

    private static void RejectDuplicateMembers(JsonElement root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw Codec(AIAssetDocumentCodecFailureReason.DuplicateMember,
                    $"Duplicate root member '{property.Name}'.", property.Name);
            }
        }
    }

    private static string RequireString(JsonElement root, string memberName)
    {
        if (!root.TryGetProperty(memberName, out var property))
        {
            throw Codec(AIAssetDocumentCodecFailureReason.MissingRequiredMember,
                $"Required member '{memberName}' is missing.", memberName);
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind,
                $"Member '{memberName}' must be a JSON string.", memberName);
        }

        return property.GetString()!;
    }

    private static string RequireSchemaVersion(JsonElement root)
    {
        if (!root.TryGetProperty("schemaVersion", out var property))
        {
            throw Codec(AIAssetDocumentCodecFailureReason.MissingRequiredMember,
                "Required member 'schemaVersion' is missing.", "schemaVersion");
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind,
                "Member 'schemaVersion' must be a JSON string.", "schemaVersion");
        }

        var value = property.GetString()!;
        if (!IsWellFormedSchemaVersion(value))
        {
            throw Codec(AIAssetDocumentCodecFailureReason.MalformedSchemaVersion,
                $"Schema version '{value}' is malformed.", "schemaVersion", value);
        }

        if (!StringComparer.Ordinal.Equals(value, "1.0"))
        {
            throw Codec(AIAssetDocumentCodecFailureReason.UnsupportedSchemaVersion,
                $"Schema version '{value}' is not supported.", "schemaVersion", value);
        }

        return value;
    }

    private static bool IsWellFormedSchemaVersion(string value)
    {
        var dot = value.IndexOf('.');
        if (dot <= 0 || dot != value.LastIndexOf('.') || dot == value.Length - 1) return false;
        return IsCanonicalDigits(value.AsSpan(0, dot)) && IsCanonicalDigits(value.AsSpan(dot + 1));
    }

    private static bool IsCanonicalDigits(ReadOnlySpan<char> value)
    {
        if (value.Length == 0 || (value.Length > 1 && value[0] == '0')) return false;
        foreach (var c in value) if (c < '0' || c > '9') return false;
        return true;
    }

    private static void EnsureValidUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw Codec(AIAssetDocumentCodecFailureReason.InvalidUnicode,
                        "String input contains invalid Unicode scalar representation.");
                }

                index++;
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                throw Codec(AIAssetDocumentCodecFailureReason.InvalidUnicode,
                    "String input contains invalid Unicode scalar representation.");
            }
        }
    }

    private static AIAssetDocumentCodecException Codec(
        AIAssetDocumentCodecFailureReason reason,
        string message,
        string? memberName = null,
        string? token = null,
        Exception? inner = null) => new(
            AIAssetDocumentCodecOperation.Deserialization,
            reason,
            message,
            memberName,
            token,
            inner);

    internal readonly record struct ParsedRoot(
        JsonElement Root,
        AIAssetDocumentRootDiscriminator.RootDescriptor Descriptor,
        string SchemaVersion);
}
