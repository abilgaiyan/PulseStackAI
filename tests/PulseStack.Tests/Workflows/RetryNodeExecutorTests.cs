using FluentAssertions;
using Xunit;
using PulseStack.Agents.Runtime;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Tests.Fakes;


namespace PulseStack.Tests.Workflows;


public class RetryNodeExecutorTests
{
    [Fact]
    public async Task RetryNode_Should_Execute_Successfully()
    {
                var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        executors.Add(
            WorkflowRuntimeFactory.CreateAgentExecutor());

        var executor =
            new RetryNodeExecutor(
                resolver);

        var retryNode = new RetryNode(
            "Retry",
            new FakeAgent(
                "Researcher",
                "Done"));

        var result =
                    await executor.ExecuteAsync(
                        retryNode,
                        new PipelineContext());

        result.Success.Should().BeTrue();
        result.Output.Should().Be("Done");                
    }
    
    [Fact]
    public async Task RetryNode_Should_Retry_Until_Success()
    {
        var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        var flakyExecutor =
            new FlakyNodeExecutor();

        executors.Add(flakyExecutor);

        var retryExecutor =
            new RetryNodeExecutor(
                resolver);

        var node =
            new RetryNode(
                "Retry",
                new FakeAgent(
                    "Researcher",
                    "Executed"),
                maxAttempts: 3);

        var result =
            await retryExecutor.ExecuteAsync(
                node,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.Output.Should().Be("Success");

        flakyExecutor.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task RetryNode_Should_Stop_After_Max_Attempts()
    {
        var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        var failingExecutor =
            new AlwaysFailNodeExecutor();

        executors.Add(failingExecutor);

        var retryExecutor =
            new RetryNodeExecutor(
                resolver);

        var node =
            new RetryNode(
                "Retry",
                new FakeAgent(
                    "Researcher",
                    "Executed"),
                maxAttempts: 3);

        var result =
            await retryExecutor.ExecuteAsync(
                node,
                new PipelineContext());

        result.Success.Should().BeFalse();

        failingExecutor.Attempts.Should().Be(3);
    }
}