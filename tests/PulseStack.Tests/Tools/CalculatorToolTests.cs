using FluentAssertions;
using PulseStack.Abstractions.Tools;
using PulseStack.Core.Tools;
using PulseStack.Core.Security; 
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
       var authorization = new AllowAllToolAuthorizationService();
        var executor =  new ToolExecutor(authorization);
        
        // Act
        var result = await executor.ExecuteAsync(
            tool,
            new ToolExecutionContext
            {
                Input = "5 * 5"
            });

        // Assert
        result.IsSuccess.Should().BeTrue();

        result.Value.Should().Be(25d);

        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Return_Invalid_For_Bad_Input()
    {
        // Arrange
        var tool = new CalculatorTool();
        var authorization = new AllowAllToolAuthorizationService();
        var executor =  new ToolExecutor(authorization);

        // Act
        var result = await executor.ExecuteAsync(
            tool,
            new ToolExecutionContext
            {
                Input = "hello"
            });


        // Assert
        result.IsSuccess.Should().BeFalse();

        result.Value.Should().BeNull();

        result.ErrorMessage.Should()
            .Contain("Invalid expression.");
    }
}