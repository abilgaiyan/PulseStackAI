namespace PulseStack.Abstractions.Runtime.Realization.Binding;

/// <summary>
/// Provides diagnostic availability checks for symbolic Workflow condition bindings.
/// </summary>
public interface IConditionBindingCatalog
{
    bool Contains(string name);
}
