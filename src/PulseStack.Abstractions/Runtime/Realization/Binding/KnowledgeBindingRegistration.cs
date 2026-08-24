using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

public sealed record KnowledgeBindingRegistration(
    AssetReference Asset,
    string SourceName);
