using PulseStack.Abstractions.Workflows.Builders;

namespace PulseStack.Tests.Workflows.Builders;

public static class TestBlockBuilderExtensions
{
    /// <summary>
    /// Starts a test composite block for validating nested builder behavior.
    /// </summary>
    public static TestBlockBuilder Test(
        this WorkflowBuilder parent, 
        string blockName = "TestBlock")
    {
        return new TestBlockBuilder(parent, blockName);
    }
}