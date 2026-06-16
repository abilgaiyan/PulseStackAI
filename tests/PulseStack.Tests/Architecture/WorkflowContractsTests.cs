using FluentAssertions;
using PulseStack.Abstractions.Runtime.Pipeline;
using Xunit;

namespace PulseStack.Tests.Architecture;

public class WorkflowContractsTests
{
    [Fact]
    public void WorkflowRuntime_Should_Be_Interface()
    {
        typeof(IWorkflowRuntime)
            .IsInterface
            .Should()
            .BeTrue();
    }

    [Fact]
    public void WorkflowExecutionResult_Should_Be_Creatable()
    {
        var result =
            new WorkflowExecutionResult
            {
                Success = true,
                FinalOutput = "Completed"
            };

        result.Success.Should().BeTrue();

        result.FinalOutput
            .Should()
            .Be("Completed");
    }
}