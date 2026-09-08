using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Core.Runtime.Realization.Binding;

public sealed class ConditionBindingResolver : IConditionBindingResolver
{
    private readonly ConditionBindingCatalog _catalog;

    public ConditionBindingResolver(ConditionBindingCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public ICondition Resolve(ConditionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition switch
        {
            NamedConditionDefinition named => ResolveNamed(named),
            _ => throw new NotSupportedException(
                $"Condition definition '{definition.GetType().Name}' is not supported by realization yet.")
        };
    }

    private ICondition ResolveNamed(NamedConditionDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        return _catalog.ResolveExact(definition.Name);
    }
}
