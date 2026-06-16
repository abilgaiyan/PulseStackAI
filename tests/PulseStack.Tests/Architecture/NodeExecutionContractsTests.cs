using FluentAssertions;
using PulseStack.Abstractions.Runtime.Pipeline;
using Xunit;

namespace PulseStack.Tests.Architecture;

public class NodeExecutionContractsTests
{
    [Fact]
    public void NodeExecutor_Should_Be_Interface()
    {
        typeof(INodeExecutor)
            .IsInterface
            .Should()
            .BeTrue();
    }

    [Fact]
    public void NodeExecutionStrategy_Should_Be_Interface()
    {
        typeof(INodeExecutionStrategy)
            .IsInterface
            .Should()
            .BeTrue();
    }
}
