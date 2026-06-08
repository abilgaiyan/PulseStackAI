using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Tests.Fakes;
using PulseStack.Agents.Pipelines;
using Xunit;

namespace PulseStack.Tests.Agents;

public class AgentPipelineTests
{
    [Fact]
    public async Task Pipeline_Should_Execute_All_Agents()
    {
        var pipeline = new SequentialPipeline("Test");

        pipeline.Add(new FakeAgent(
            "Researcher",
            "Research result"));

        pipeline.Add(new FakeAgent(
            "Writer",
            "Final summary"));

        var result = await pipeline.RunAsync("AI");

        result.FinalOutput.Should().Be("Final summary");
    }

    [Fact]
    public async Task RunDetailedAsync_Should_Return_Public_Execution_Result()
    {
        var pipeline = new SequentialPipeline("Detailed")
            .Add(new FakeAgent("Researcher", "Research result"))
            .Add(new FakeAgent("Writer", "Final summary"));

        var result = await pipeline.RunDetailedAsync("AI");

        result.Success.Should().BeTrue();
        result.Status.Should().Be(ExecutionStatus.Completed);
        result.ExecutionId.Should().NotBeEmpty();
        result.Duration.Should().Be(result.CompletedAt - result.StartedAt);
        result.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        result.FinalOutput.Should().Be("Final summary");
        result.Steps.Select(step => step.AgentName)
            .Should()
            .Equal("Researcher", "Writer");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RunDetailedAsync_Should_Propagate_Execution_Errors()
    {
        var dispatcher = new RuntimeEventDispatcher();
        var pipeline = new ParallelPipeline("DetailedErrors", dispatcher)
            .Add(new FakeAgent("Researcher", "Research result"))
            .Add(new ThrowingAgent("Broken"));

        var result = await pipeline.RunDetailedAsync("AI");

        result.Success.Should().BeFalse();
        result.Status.Should().Be(ExecutionStatus.PartialSuccess);
        result.FinalOutput.Should().Be("Research result");
        result.Errors.Should().ContainSingle();
        result.Errors[0].Code.Should().Be("parallel_agent_execution_failed");
        result.Errors[0].AgentName.Should().Be("Broken");
        result.Errors[0].Message.Should().Be("Agent failed.");
    }

    [Fact]
    public async Task RunAsync_Should_Remain_Backward_Compatible()
    {
        var dispatcher = new RuntimeEventDispatcher();
        var pipeline = new ParallelPipeline("Compatibility", dispatcher)
            .Add(new FakeAgent("First", "one"))
            .Add(new FakeAgent("Second", "two"));

        var result = await pipeline.RunAsync("input");

        result.FinalOutput.Should().Be(
            string.Join(Environment.NewLine, "one", "two"));
        result.Steps.Select(step => step.AgentName)
            .Should()
            .Equal("First", "Second");
    }

    private sealed class ThrowingAgent
        : IAgent
    {
        public ThrowingAgent(
            string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string? Model => null;

        public Task<ChatResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            var context = new PipelineContext
            {
                Input = input,
                CurrentOutput = input
            };

            return RunAsync(
                context,
                cancellationToken);
        }

        public Task<ChatResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Agent failed.");

        public async IAsyncEnumerable<string> StreamAsync(
            string input,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            yield return string.Empty;

            await Task.CompletedTask;
        }
    }
}
