using PulseStack.Abstractions.Agents;
using  PulseStack.Abstractions.Runtime.Pipeline;
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
public abstract class CompositeWorkflowBuilder<TParent> 
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

    /// <summary>
    /// Executes a single declarative agent behavior within this nested block.
    /// </summary>
    public CompositeWorkflowBuilder<TParent> Run(IAgent agent)
    {

        AddStep(new RunStep(agent));
        return this;
    }

    /// <summary>
    /// Implements a complete child workflow sequence directly within this nested block.
    /// </summary>
    public CompositeWorkflowBuilder<TParent> Workflow(Workflow workflow)
    {
        AddStep(workflow);
        return this;
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

    /// <summary>
    /// Terminates the local block configuration, packages the compiled step composition,
    /// and safely reverts fluent execution back to the parent builder.
    /// </summary>
    public abstract TParent End();
}
