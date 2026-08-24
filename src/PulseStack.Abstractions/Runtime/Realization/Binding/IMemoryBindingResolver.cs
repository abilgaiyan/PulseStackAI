using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Memory;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

public interface IMemoryBindingResolver
{
    IConversationMemory Resolve(MemoryAsset asset);
}
