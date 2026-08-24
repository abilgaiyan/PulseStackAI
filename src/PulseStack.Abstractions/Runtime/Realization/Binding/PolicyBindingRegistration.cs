using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

public sealed record PolicyBindingRegistration(
    AssetReference Asset,
    string PolicyName);
