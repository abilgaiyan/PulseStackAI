
namespace PulseStack.Abstractions.Assets;

public readonly record struct AssetId(Guid Value)
{
    public static AssetId New() => new(Guid.NewGuid());

    public static AssetId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString() => Value.ToString();

    public void EnsureValid()
    {
        if (IsEmpty)
        {
            throw new ArgumentException(
                "AssetId cannot be empty.");
        }
    }
}