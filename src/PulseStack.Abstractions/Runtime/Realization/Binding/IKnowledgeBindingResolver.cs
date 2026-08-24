using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Knowledge;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

public interface IKnowledgeBindingResolver
{
    IKnowledgeSource Resolve(KnowledgeAsset asset);
}
