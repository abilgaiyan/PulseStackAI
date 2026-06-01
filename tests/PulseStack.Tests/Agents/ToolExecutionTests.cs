using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Builders;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Tools;
using PulseStack.Core.Security;
using PulseStack.Core.Tools;
using PulseStack.Tools.BuiltIn;
using PulseStack.Tests.Fakes;
using CoreToolExecutor = PulseStack.Core.Tools.ToolExecutor;
using ToolResultRecord = PulseStack.Abstractions.Tools.ToolExecutionRecord;

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
        var executor = new CoreToolExecutor(authorization);

        var agent = new AgentBuilder("Assistant", client, executor)
            .WithTools(registry)
            .Build();

        // Act
        var result = await agent.RunAsync(
            "What is 5 * 5?");

        // Assert
        result.Text.Should().Be("The result is 25.");
    }

    [Fact]
    public async Task ToolExecutor_Should_Populate_Metadata()
    {
        var tool = new CalculatorTool();
        var executor = new CoreToolExecutor(
            new AllowAllToolAuthorizationService());

        var result =
            await executor.ExecuteAsync(
                tool,
                new ToolExecutionContext
                {
                    ToolName = tool.Name,
                    Input = "5 * 5"
                });

        result.IsSuccess.Should().BeTrue();
        result.Metadata.ToolName.Should().Be("calculator");
        result.Metadata.Category.Should().Be("Utility");
        result.Metadata.Success.Should().BeTrue();
        result.Metadata.CompletedAt.Should().BeOnOrAfter(result.Metadata.StartedAt);
        result.Metadata.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void ToolExecutionAggregator_Should_Build_Summary()
    {
        var aggregator = new ToolExecutionAggregator();

        var summary =
            aggregator.Aggregate(
            [
                new ToolResultRecord(
                    "calculator",
                    "input",
                    new ToolExecutionResult
                    {
                        IsSuccess = true,
                        Metadata = new ToolExecutionMetadata
                        {
                            ToolName = "calculator",
                            Category = "Utility",
                            Success = true,
                            Duration = TimeSpan.FromMilliseconds(10)
                        }
                    }),
                new ToolResultRecord(
                    "lookup",
                    "input",
                    new ToolExecutionResult
                    {
                        IsSuccess = false,
                        Metadata = new ToolExecutionMetadata
                        {
                            ToolName = "lookup",
                            Category = "ERP",
                            Success = false,
                            Duration = TimeSpan.FromMilliseconds(5)
                        }
                    })
            ]);

        summary.TotalInvocations.Should().Be(2);
        summary.SuccessfulExecutions.Should().Be(1);
        summary.FailedExecutions.Should().Be(1);
        summary.TotalDuration.Should().Be(TimeSpan.FromMilliseconds(15));
        summary.Executions.Select(execution => execution.ToolName)
            .Should()
            .Equal("calculator", "lookup");
    }

    [Fact]
    public async Task PipelineExecutionResult_Should_Contain_Tool_Metrics()
    {
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

        var agent = new AgentBuilder(
                "Assistant",
                client,
                new CoreToolExecutor(new AllowAllToolAuthorizationService()))
            .WithTools(registry)
            .Build();

        var result =
            await new SequentialPipeline("ToolMetrics")
                .Add(agent)
                .RunDetailedAsync(
                    new PipelineContext
                    {
                        Input = "What is 5 * 5?",
                        CurrentOutput = "What is 5 * 5?"
                    });

        result.ToolSummary.TotalInvocations.Should().Be(1);
        result.ToolSummary.SuccessfulExecutions.Should().Be(1);
        result.ToolSummary.FailedExecutions.Should().Be(0);
        result.ToolSummary.TotalDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        result.ToolSummary.Executions.Should().ContainSingle()
            .Which.ToolName.Should().Be("calculator");
    }
}
