
namespace PulseStack.Abstractions.Assets;
public sealed record AssetDependency
(
    AssetReference Reference,

    bool Required = true
);