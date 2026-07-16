using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Routing;
using PulseStack.Abstractions.Workflows.Language;

namespace PulseStack.Abstractions.Workflows.Builders;

public sealed class WorkflowBuilder 
    : IWorkflowBuilderParent<WorkflowBuilder>
{
    private readonly Workflow _workflow;

    internal WorkflowBuilder(
        string name)
    {
         ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _workflow =
            new Workflow(
                name);
    }

    /// <summary>
    /// Implementation of IWorkflowBuilderParent<WorkflowBuilder>
    /// </summary>
    public WorkflowBuilder AddStep(IWorkflowStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _workflow.Add(step);
        return this;
    }

    public WorkflowBuilder Run(
        IAgent agent)
    {
       return AddStep(new RunStep(agent));
    }

    public WorkflowBuilder Workflow(
        Workflow workflow)
    {
       return AddStep(workflow);
    }
    
    /// <summary>
    /// Begins a conditional workflow block.
    /// The next valid language construct is Then().
    /// </summary>
    public IfConditionBuilder<WorkflowBuilder> If(
        ICondition condition)
    {
        return If(WorkflowKeywords.If, condition);
    }

    /// <summary>
    /// Begins a named conditional workflow block.
    /// The next valid language construct is Then().
    /// </summary>
    public IfConditionBuilder<WorkflowBuilder> If(
        string name,
        ICondition condition)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(condition);

        return new IfConditionBuilder<WorkflowBuilder>(
            this,
            name,
            condition);
    }

    /// <summary>
    /// Adds a conditional branch to the workflow (uses default name WorkflowKeywords.If).
    /// </summary>
    [Obsolete("Use the Workflow Language syntax: If(...).Then()...End().")]
    public WorkflowBuilder If(
        ICondition condition,
        IWorkflowStep thenStep) => If(WorkflowKeywords.If, condition, thenStep);

    /// <summary>
    /// Adds a conditional branch to the workflow with a custom name.
    /// The name will appear in diagnostics, logs, and future workflow visualizations.
    /// </summary>
    [Obsolete("Use the Workflow Language syntax: If(...).Then()...End().")]
    public WorkflowBuilder If(
        string name,
        ICondition condition,
        IWorkflowStep thenStep)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenStep);

        return AddStep(
            new ConditionalStep(
                name,
                condition,
                thenStep));
    }

    [Obsolete("Use the Workflow Language syntax: If(...).Then()...End().")]
    public WorkflowBuilder If(
        ICondition condition,
        IAgent agent)
        => If(WorkflowKeywords.If, condition, agent);

    [Obsolete("Use the Workflow Language syntax: If(...).Then()...End().")]
    public WorkflowBuilder If(
        string name,
        ICondition condition,
        IAgent agent)
    {
        return If(
            name,
            condition,
            new RunStep(agent));
    }

    /// <summary>
    /// Wraps a step with retry logic (default name "Retry")
    /// </summary>
    public WorkflowBuilder Retry(
        IWorkflowStep step,
        int maxAttempts = 3) => Retry("Retry", step, maxAttempts);
    
    /// <summary>
    /// Wraps a step with retry logic using a custom name.
    /// </summary>
    public WorkflowBuilder Retry(
        string name,
        IWorkflowStep step,
        int maxAttempts = 3)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(step);

        ValidateRetryAttempts(maxAttempts);

        return AddStep(
            new RetryStep(
                name, 
                step, 
                maxAttempts));
    }

    /// <summary>
    /// Creates a ForEach loop (default name "ForEach")
    /// </summary>
    public WorkflowBuilder ForEach(
        Func<PipelineContext, IEnumerable<object>> items,
        IWorkflowStep step) => ForEach("ForEach", items, step);

    /// <summary>
    /// Creates a ForEach loop with a custom name.
    /// </summary>
    public WorkflowBuilder ForEach(
        string name,
        Func<PipelineContext, IEnumerable<object>> items,
        IWorkflowStep step)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(step);

        return AddStep(
            new LoopStep(
                name, 
                items, 
                step));
    }

    /// <summary>
    /// Creates a Parallel execution block (default name "Parallel").
    /// </summary>
    public WorkflowBuilder Parallel(params IWorkflowStep[] steps)
        => Parallel("Parallel", steps);

    /// <summary>
    /// Creates a Parallel execution block with a custom name.
    /// The name will appear in diagnostics, logs, and future workflow visualizations.
    /// </summary>
    public WorkflowBuilder Parallel(string name, params IWorkflowStep[] steps)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(steps);

        ValidateSteps(steps);

        var parallel = 
            new ParallelStep(
                name);
                
        foreach (var step in steps)
            parallel.Add(step);

        return AddStep(parallel);
    }

    public ParallelBuilder Parallel()
    => Parallel("Parallel");

    public ParallelBuilder Parallel(string name)
    {
        ValidateName(name);

        return new ParallelBuilder(this, name);
    }

    /// <summary>
    /// Adds a switch branch to the workflow (default name "Switch").
    /// </summary>
    public WorkflowBuilder Switch(
        Func<PipelineContext, string?> selector,
        IEnumerable<SwitchCase> cases,
        IWorkflowStep? defaultStep = null)
        => Switch("Switch", selector, cases, defaultStep);

    /// <summary>
    /// Adds a switch branch to the workflow with a custom name.
    /// The name will appear in diagnostics, logs, and future workflow visualizations.
    /// </summary>
    public WorkflowBuilder Switch(
        string name,
        Func<PipelineContext, string?> selector,
        IEnumerable<SwitchCase> cases,
        IWorkflowStep? defaultStep = null)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(cases);

        var caseList =
            cases as IReadOnlyList<SwitchCase>
            ?? cases.ToArray();

        ValidateSwitchCases(caseList);
        
        return AddStep(
            new SwitchStep(
                name,
                selector,
                caseList,
                defaultStep));
    }

    public Workflow Build()
    {
        return _workflow;
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
    }

    private static void ValidateRetryAttempts(
        int maxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            maxAttempts,
            1);
    }

    private static void ValidateSteps(
        IReadOnlyList<IWorkflowStep> steps)
    {
         if (steps.Count == 0)
            throw new ArgumentException(
                "At least one step is required for Parallel execution.",
                nameof(steps));

        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i] is null)
                throw new ArgumentNullException(
                    nameof(steps),
                     $"Step at index {i} cannot be null.");
        }

    }

    private static void ValidateSwitchCases(
        IReadOnlyList<SwitchCase> caseList)
    {
         if (caseList.Count == 0)
            throw new ArgumentException(
                "Switch requires at least one case.",
                nameof(caseList));

        for (var i = 0; i < caseList.Count; i++)
        {
            if (caseList[i] is null)
                throw new ArgumentNullException(
                    nameof(caseList),
                     $"SwitchCase at index {i} cannot be null.");
        }

    }
}
