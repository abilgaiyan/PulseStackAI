using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

public sealed record MemoryBindingRegistration(
    AssetReference Asset,
    string FactoryName);
