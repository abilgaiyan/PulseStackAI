using FluentAssertions;
using PulseStack.Abstractions.Workflows.Steps;
using Xunit;

namespace PulseStack.Tests.Architecture;

public class StepExecutionResultTests
{
    [Fact]
    public void Should_Create_Result()
    {
        var result =
            new StepExecutionResult
            {
                StepName = "TestWorkflowStep",
                Success = true,
                Output = "Completed"
            };

        result.StepName.Should().Be("TestWorkflowStep");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Completed");
    }
}
