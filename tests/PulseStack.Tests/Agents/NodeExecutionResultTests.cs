using FluentAssertions;
using PulseStack.Abstractions.Workflow.Nodes;
using Xunit;

namespace PulseStack.Tests.Architecture;

public class NodeExecutionResultTests
{
    [Fact]
    public void Should_Create_Result()
    {
        var result =
            new NodeExecutionResult
            {
                NodeName = "TestNode",
                Success = true,
                Output = "Completed"
            };

        result.NodeName.Should().Be("TestNode");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Completed");
    }
}
