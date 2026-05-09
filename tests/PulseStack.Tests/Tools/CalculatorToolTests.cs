using FluentAssertions;
using PulseStack.Tools.BuiltIn;
using Xunit;

namespace PulseStack.Tests.Tools;

public class CalculatorToolTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Return_Correct_Result()
    {
        var tool = new CalculatorTool();

        var result = await tool.ExecuteAsync("5 * 5");

        result.Should().Be("25");
    }

    [Fact]
    public async Task ExecuteAsync_Should_Return_Invalid_For_Bad_Input()
    {
        var tool = new CalculatorTool();

        var result = await tool.ExecuteAsync("hello");

        result.Should().Be("Invalid expression.");
    }
}