using FluentAssertions;
using PulseStack.Tools.BuiltIn;
using Xunit;

namespace PulseStack.Tests.Tools;

public class CalculatorToolTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Return_Correct_Result()
    {
        // Arrange
        var tool = new CalculatorTool();

        // Act
        var result = await tool.ExecuteAsync("5 * 5");

        // Assert
        result.Success.Should().BeTrue();

        result.Output.Should().Be("25");

        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Return_Invalid_For_Bad_Input()
    {
        // Arrange
        var tool = new CalculatorTool();

        // Act
        var result = await tool.ExecuteAsync("hello");

        // Assert
        result.Success.Should().BeFalse();

        result.Output.Should().BeEmpty();

        result.Error.Should().Be("Invalid expression.");
    }
}