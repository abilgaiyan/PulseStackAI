using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

/// <summary>
/// Explicitly binds a Tool Asset to a registered runtime Tool implementation.
/// </summary>
public sealed record ToolBindingRegistration(
    AssetReference Asset,
    string ToolName);
