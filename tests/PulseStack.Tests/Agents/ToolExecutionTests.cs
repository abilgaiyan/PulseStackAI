using Xunit;
using FluentAssertions;
using PulseStack.Agents.Builders;
using PulseStack.Core.Tools;
using PulseStack.Core.Security;
using PulseStack.Tools.BuiltIn;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Agents;

public class ToolExecutionTests
{
    [Fact]
    public async Task RunAsync_Should_Execute_Tool_And_Return_Final_Response()
    {
        // Arrange
        var registry = new ToolRegistry();

        registry.Register(new CalculatorTool());

        var client = new FakeChatClient([
            """
            {
              "tool": "calculator",
              "input": "5 * 5"
            }
            """,
            "The result is 25."
        ]);

        var authorization = new AllowAllToolAuthorizationService();
        var executor =  new ToolExecutor(authorization);

        var agent = new AgentBuilder("Assistant", client, executor)
            .WithTools(registry)
            .Build();

        // Act
        var result = await agent.RunAsync(
            "What is 5 * 5?");

        // Assert
        result.Text.Should().Be("The result is 25.");
    }
}