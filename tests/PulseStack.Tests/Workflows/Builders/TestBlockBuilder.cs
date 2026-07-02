using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Tests.Workflows.Builders;

/// <summary>
/// Test-only builder used to validate CompositeWorkflowBuilder and nested builder patterns.
/// </summary>
public sealed class TestBlockBuilder 
    : CompositeWorkflowBuilder<WorkflowBuilder>
{
    private readonly string _blockName;

    public TestBlockBuilder(WorkflowBuilder parent, string blockName = "TestBlock")
        : base(parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockName);
        _blockName = blockName;
    }

    public override WorkflowBuilder End()
    {
        var childWorkflow = new WorkflowPipeline(_blockName);

        foreach (var node in Nodes)
        {
            childWorkflow.Add(node);
        }

        return Parent.AddNode(childWorkflow);
    }
}
