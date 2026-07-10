using FluentAssertions;
using PulseStack.Abstractions.Runtime.Pipeline;
using Xunit;

namespace PulseStack.Tests.Architecture;

public class StepExecutionContractsTests
{
    [Fact]
    public void StepExecutor_Should_Be_Interface()
    {
        typeof(IStepExecutor)
            .IsInterface
            .Should()
            .BeTrue();
    }

    [Fact]
    public void StepExecutionStrategy_Should_Be_Interface()
    {
        typeof(IStepExecutionStrategy)
            .IsInterface
            .Should()
            .BeTrue();
    }
}
