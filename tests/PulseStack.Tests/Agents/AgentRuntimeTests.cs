using FluentAssertions;
using PulseStack.Agents.Runtime;
using PulseStack.Core.Security;
using PulseStack.Core.Tools;
using PulseStack.Tools.BuiltIn;
using PulseStack.Abstractions.Agents;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Agents;

public class AgentRuntimeTests
{
    [Fact]
    public async Task RunAsync_Should_Execute_Tool_And_Update_Context()
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
        var executor = new ToolExecutor(authorization);

        var runtime = new AgentRuntime(
            client,
            executor,
            instructions: null,
            temperature: null,
            tools: registry);

        var context = new PipelineContext
        {
            Input = "What is 5 * 5?",
            CurrentOutput = "What is 5 * 5?"
        };

        // Act
        var result = await runtime.RunAsync(context);

        // Assert
        result.Text.Should().Be("The result is 25.");
        context.CurrentOutput.Should().Be("The result is 25.");
    }
}
