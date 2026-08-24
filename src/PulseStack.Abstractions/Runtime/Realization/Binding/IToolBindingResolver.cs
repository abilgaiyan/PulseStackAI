using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

public interface IToolBindingResolver
{
    ITool Resolve(ToolAsset asset);
}
