using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Abstractions.Runtime.Realization.Binding;

/// <summary>
/// Associates a Workflow-language condition name with a runtime condition.
/// </summary>
public sealed record ConditionBindingRegistration(
    string Name,
    ICondition Condition);
