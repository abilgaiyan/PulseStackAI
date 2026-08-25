using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Core.Runtime.Realization.Binding;

public sealed class ConditionBindingResolver : IConditionBindingResolver
{
    private readonly IReadOnlyDictionary<string, ICondition> _conditions;

    public ConditionBindingResolver(
        IEnumerable<ConditionBindingRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        _conditions = registrations.ToDictionary(
            registration => registration.Name,
            registration => registration.Condition,
            StringComparer.OrdinalIgnoreCase);
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

        if (_conditions.TryGetValue(definition.Name, out var condition))
        {
            return condition;
        }

        throw new InvalidOperationException(
            $"Runtime condition '{definition.Name}' is not registered.");
    }
}
