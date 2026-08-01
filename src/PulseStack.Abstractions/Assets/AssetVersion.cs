
namespace PulseStack.Abstractions.Assets;
public sealed record AssetVersion(string Value)
{
    public static AssetVersion Initial => new("1.0.0");
}