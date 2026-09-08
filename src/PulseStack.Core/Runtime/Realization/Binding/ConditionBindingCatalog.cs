using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Core.Runtime.Realization.Binding;

public sealed class ConditionBindingCatalog : IConditionBindingCatalog
{
    private readonly IReadOnlyDictionary<string, ICondition> _conditions;

    public ConditionBindingCatalog(
        IEnumerable<ConditionBindingRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var snapshot = registrations.ToArray();
        var conditions = new Dictionary<string, ICondition>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < snapshot.Length; index++)
        {
            var registration = snapshot[index]
                ?? throw new ArgumentException(
                    $"Condition binding registration at index {index} cannot be null.",
                    nameof(registrations));

            if (string.IsNullOrWhiteSpace(registration.Name))
            {
                throw new ArgumentException(
                    $"Condition binding registration at index {index} must have a non-blank name.",
                    nameof(registrations));
            }

            if (registration.Condition is null)
            {
                throw new ArgumentException(
                    $"Condition binding registration '{registration.Name}' must have a condition instance.",
                    nameof(registrations));
            }

            if (!conditions.TryAdd(registration.Name, registration.Condition))
            {
                throw new InvalidOperationException(
                    $"Condition binding '{registration.Name}' is registered more than once under case-insensitive comparison.");
            }
        }

        _conditions = conditions;
    }

    public bool Contains(string name)
        => !string.IsNullOrWhiteSpace(name)
            && _conditions.ContainsKey(name);

    internal ICondition ResolveExact(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_conditions.TryGetValue(name, out var condition))
        {
            return condition;
        }

        throw new InvalidOperationException(
            $"Runtime condition '{name}' is not registered.");
    }
}
