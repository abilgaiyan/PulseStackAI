using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;

namespace PulseStack.Core.Assets;

/// <summary>
/// Explicit-identity authoring authority for recursively identity-complete
/// declarative workflow-step subtrees.
/// </summary>
public static class DurableWorkflowStep
{
    public static IdentityCompleteWorkflowStep Run(
        WorkflowStepId id,
        AssetReference agent)
    {
        EnsureValidId(id);
        ArgumentNullException.ThrowIfNull(agent);

        return Complete(new RunStepDefinition
        {
            Id = id,
            Agent = agent
        });
    }

    public static IdentityCompleteWorkflowStep Parallel(
        WorkflowStepId id,
        string name,
        IEnumerable<IdentityCompleteWorkflowStep> steps)
    {
        EnsureValidId(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var children = SnapshotSteps(steps, nameof(steps));

        return Complete(new ParallelStepDefinition
        {
            Id = id,
            Name = name,
            Steps = children.Select(static child => child.Definition).ToArray()
        });
    }

    public static IdentityCompleteWorkflowStep Conditional(
        WorkflowStepId id,
        string name,
        ConditionDefinition condition,
        IdentityCompleteWorkflowStep thenStep,
        IdentityCompleteWorkflowStep? elseStep = null)
    {
        EnsureValidId(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenStep);

        return Complete(new ConditionalStepDefinition
        {
            Id = id,
            Name = name,
            Condition = condition,
            ThenStep = thenStep.Definition,
            ElseStep = elseStep?.Definition
        });
    }

    public static IdentityCompleteWorkflowStep Retry(
        WorkflowStepId id,
        IdentityCompleteWorkflowStep step,
        int maxAttempts = 3,
        string name = "Retry")
    {
        EnsureValidId(id);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Complete(new RetryStepDefinition
        {
            Id = id,
            Name = name,
            Step = step.Definition,
            MaxAttempts = maxAttempts
        });
    }

    public static IdentityCompleteWorkflowStep Loop(
        WorkflowStepId id,
        WorkflowValueDefinition items,
        IdentityCompleteWorkflowStep step,
        string name = "ForEach")
    {
        EnsureValidId(id);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Complete(new LoopStepDefinition
        {
            Id = id,
            Name = name,
            Items = items,
            Step = step.Definition
        });
    }

    public static IdentityCompleteSwitchCase SwitchCase(
        string value,
        IdentityCompleteWorkflowStep step)
        => new(value, step);

    public static IdentityCompleteWorkflowStep Switch(
        WorkflowStepId id,
        WorkflowValueDefinition selector,
        IEnumerable<IdentityCompleteSwitchCase> cases,
        IdentityCompleteWorkflowStep? defaultStep = null,
        string name = "Switch")
    {
        EnsureValidId(id);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(cases);

        var caseSnapshot = cases.ToArray();
        if (caseSnapshot.Any(static @case => @case is null))
        {
            throw new ArgumentException("Switch cases cannot contain null entries.", nameof(cases));
        }

        return Complete(new SwitchStepDefinition
        {
            Id = id,
            Name = name,
            Selector = selector,
            Cases = caseSnapshot
                .Select(static @case => new SwitchCaseDefinition
                {
                    Value = @case.Value,
                    Step = @case.Step.Definition
                })
                .ToArray(),
            DefaultStep = defaultStep?.Definition
        });
    }

    private static IdentityCompleteWorkflowStep Complete(WorkflowStepDefinition definition)
        => new(definition);

    private static IdentityCompleteWorkflowStep[] SnapshotSteps(
        IEnumerable<IdentityCompleteWorkflowStep> steps,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(steps, parameterName);

        var snapshot = steps.ToArray();
        if (snapshot.Any(static step => step is null))
        {
            throw new ArgumentException(
                "Identity-complete workflow-step collections cannot contain null entries.",
                parameterName);
        }

        return snapshot;
    }

    private static void EnsureValidId(WorkflowStepId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow step identity must contain a non-empty GUID.",
                nameof(id));
        }
    }
}
