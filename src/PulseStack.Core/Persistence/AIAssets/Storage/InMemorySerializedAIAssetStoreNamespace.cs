using System.Collections.Concurrent;
using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Persistence.AIAssets.Storage;

/// <summary>
/// Represents one logical in-memory serialized AI Asset namespace shared by one or more store instances.
/// </summary>
public sealed class InMemorySerializedAIAssetStoreNamespace
{
    internal ConcurrentDictionary<AssetDefinitionKey, byte[]> Storage { get; } = new();
}
