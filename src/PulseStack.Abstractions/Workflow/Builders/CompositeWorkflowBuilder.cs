using PulseStack.Abstractions.Agents;
using  PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Abstractions.Workflow.Builders;

/// <summary>
/// Base class for all nested workflow language blocks.
///
/// A CompositeWorkflowBuilder represents a temporary language scope.
/// It collects child nodes while the workflow is being authored.
/// When End() is called, the collected nodes are packaged into a
/// concrete workflow node and returned to the parent builder.
///
/// Builders never execute workflows.
/// They only describe them.
/// A CompositeWorkflowBuilder exists only while a workflow
/// is being authored. Once End() is called, the builder
/// disappears and only the resulting workflow node remains.
/// </summary>
public abstract class CompositeWorkflowBuilder<TParent> 
    where TParent : IWorkflowBuilderParent<TParent>
{
     /// <summary>
    /// Parent language scope.
    /// </summary>
    protected TParent Parent { get; }

    /// <summary>
    /// Nodes authored within this scope.
    /// </summary>
    protected IReadOnlyList<IPipelineNode> Nodes => _nodes;

    private readonly List<IPipelineNode> _nodes = new();

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

        AddNode(agent);
        return this;
    }

    /// <summary>
    /// Implements a complete child workflow sequence directly within this nested block.
    /// </summary>
    public CompositeWorkflowBuilder<TParent> Workflow(WorkflowDefinition workflow)
    {
        AddNode(workflow);
        return this;
    }

     /// <summary>
    /// Adds a node to this scope.
    /// </summary>
    protected void AddNode(
        IPipelineNode node)
    {
        
        ArgumentNullException.ThrowIfNull(node);

        _nodes.Add(node);
    }

    /// <summary>
    /// Terminates the local block configuration, packages the compiled node composition,
    /// and safely reverts fluent execution back to the parent builder.
    /// </summary>
    public abstract TParent End();
}
