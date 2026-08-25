using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

/// <summary>
/// Binds declarative Workflow condition grammar to executable runtime conditions.
/// </summary>
public interface IConditionBindingResolver
{
    ICondition Resolve(ConditionDefinition definition);
}
