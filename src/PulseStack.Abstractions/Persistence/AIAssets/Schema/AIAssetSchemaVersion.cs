namespace PulseStack.Abstractions.Persistence.AIAssets.Schema;

/// <summary>
/// Identifies the version of the canonical AI Asset persistence schema.
/// </summary>
public sealed record AIAssetSchemaVersion(string Value)
{
    /// <summary>
    /// Initial canonical AI Asset persistence schema.
    /// </summary>
    public static AIAssetSchemaVersion V1 { get; } = new("1.0");

    public override string ToString() => Value;
}
