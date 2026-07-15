using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Workflows.Builders;

/// <summary>
/// Base class for all nested workflow language blocks.
///
/// A CompositeWorkflowBuilder represents a temporary language scope.
/// It collects child steps while the workflow is being authored.
/// When End() is called, the collected steps are packaged into a
/// concrete workflow step and returned to the parent builder.
///
/// Builders never execute workflows.
/// They only describe them.
/// A CompositeWorkflowBuilder exists only while a workflow
/// is being authored. Once End() is called, the builder
/// disappears and only the resulting workflow step remains.
/// </summary>
public abstract class CompositeWorkflowBuilder<TBuilder, TParent>
    where TBuilder : CompositeWorkflowBuilder<TBuilder, TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    /// <summary>
    /// Parent language scope.
    /// </summary>
    protected TParent Parent { get; }

    /// <summary>
    /// Steps authored within this scope.
    /// </summary>
    protected IReadOnlyList<IWorkflowStep> Steps => _steps;

    private readonly List<IWorkflowStep> _steps = new();

    protected CompositeWorkflowBuilder(TParent parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        Parent = parent;
    }
    protected TBuilder This => (TBuilder)this;

    /// <summary>
    /// Executes a single declarative agent behavior within this nested block.
    /// </summary>
   public TBuilder Run(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        AddStep(new RunStep(agent));

        return This;
    }

    /// <summary>
    /// Implements a complete child workflow sequence directly within this nested block.
    /// </summary>
    public TBuilder Workflow(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        AddStep(workflow);

        return This;
    }

    /// <summary>
    /// Adds a step to this scope.
    /// </summary>
    protected void AddStep(
        IWorkflowStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        _steps.Add(step);
    }


    #region Compiler
    /// <summary>
    /// Compiler helpers used by language builders.
    ///
    /// These methods translate temporary language scopes into
    /// immutable workflow model objects consumed by the
    /// Workflow Runtime.
    /// </summary>
    protected Workflow CompileWorkflow(
        string name)
    {
        var workflow = new Workflow(name);

        foreach (var step in Steps)
        {
            workflow.Add(step);
        }

        return workflow;
    }

   protected ParallelStep CompileParallel(
        string name)
    {
        if (Steps.Count == 0)
        {
            throw new InvalidOperationException(
                "Parallel block requires at least one workflow step.");
        }

        var parallel =
            new ParallelStep(name);

        foreach (var step in Steps)
        {
            parallel.Add(step);
        }

        return parallel;
    }

    #endregion

    /// <summary>
    /// Terminates the local block configuration, packages the compiled step composition,
    /// and safely reverts fluent execution back to the parent builder.
    /// </summary>
    public abstract TParent End();
}
