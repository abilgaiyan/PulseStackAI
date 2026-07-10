using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Builders;

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
        var childWorkflow = new Workflow(_blockName);

        foreach (var step in Steps)
        {
            childWorkflow.Add(step);
        }

        return Parent.AddStep(childWorkflow);
    }
}
