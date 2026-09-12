namespace PulseStack.Core.Persistence.AIAssets.Catalog;

/// <summary>
/// Shared strict Unicode representation checks for catalog durable representations.
/// No normalization, replacement, or repair is performed.
/// </summary>
internal static class AIAssetCatalogUnicode
{
    public static void EnsureWellFormed(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

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
                throw new ArgumentException(
                    "Catalog representation strings must contain well-formed Unicode.",
                    parameterName);
            }

            index++;
        }
    }
}
