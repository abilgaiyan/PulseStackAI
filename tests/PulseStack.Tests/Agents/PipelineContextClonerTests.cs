using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Runtime.Context;
using Xunit;

namespace PulseStack.Tests.Agents;

public class PipelineContextClonerTests
{
    [Fact]
    public void Clone_Should_Copy_PipelineContext_State_Into_New_Collections()
    {
        // Arrange
        var sharedItem = new List<string> { "value" };
        var step = new PipelineStepResult(
            "Agent",
            "model",
            "input",
            "output");
        var toolResult = new ToolExecutionRecord(
            "calculator",
            "1 + 1",
            ToolExecutionResult.Success("2"));

        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "output"
        };

        context.Items["shared"] = sharedItem;
        context.Steps.Add(step);
        context.ToolResults.Add(toolResult);

        var cloner = new PipelineContextCloner();

        // Act
        var clone = cloner.Clone(context);

        // Assert
        clone.Should().NotBeSameAs(context);
        clone.Input.Should().Be(context.Input);
        clone.CurrentOutput.Should().Be(context.CurrentOutput);

        clone.Items.Should().NotBeSameAs(context.Items);
        clone.Steps.Should().NotBeSameAs(context.Steps);
        clone.ToolResults.Should().NotBeSameAs(context.ToolResults);

        clone.Items.Should().ContainKey("shared");
        clone.Items["shared"].Should().BeSameAs(sharedItem);
        clone.Steps.Should().ContainSingle().Which.Should().Be(step);
        clone.ToolResults.Should().ContainSingle().Which.Should().Be(toolResult);
    }
}
