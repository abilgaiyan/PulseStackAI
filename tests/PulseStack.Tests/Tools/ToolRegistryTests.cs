using FluentAssertions;
using PulseStack.Core.Tools;
using PulseStack.Tools.BuiltIn;
using Xunit;

namespace PulseStack.Tests.Tools;

public class ToolRegistryTests
{
    [Fact]
    public void Register_Should_Add_Tool()
    {
        var registry = new ToolRegistry();

        var tool = new CalculatorTool();

        registry.Register(tool);

        registry.GetByName("calculator")
            .Should()
            .NotBeNull();
    }

    [Fact]
    public void GetByTag_Should_Return_Matching_Tools()
    {
        var registry = new ToolRegistry();

        registry.Register(new CalculatorTool());

        var tools = registry.GetByTag("math");

        tools.Should().HaveCount(1);
    }
}