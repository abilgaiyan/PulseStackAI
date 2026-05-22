using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using Xunit;

namespace PulseStack.Tests.Agents;

public class PipelineRuntimeTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Return_Execution_Snapshot()
    {
        var dispatcher = new RuntimeEventDispatcher();
        var runtime = new PipelineRuntime(dispatcher);
        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };
        var agents = new IAgent[]
        {
            new StaticAgent("First", "one"),
            new StaticAgent("Second", "two")
        };

        var result = await runtime.ExecuteAsync(
            "Sequential",
            agents,
            context,
            new SequentialPipelineExecutionStrategy());

        result.Success.Should().BeTrue();
        result.ExecutionId.Should().NotBeEmpty();
        result.FinalOutput.Should().Be("two");
        result.Steps.Select(s => s.AgentName)
            .Should()
            .Equal("First", "Second");
        result.Errors.Should().BeEmpty();
        result.StartedAt.Should().BeOnOrBefore(result.CompletedAt);
        result.Duration.Should().Be(result.CompletedAt - result.StartedAt);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Publish_Diagnostics_And_Preserve_Metadata()
    {
        var dispatcher = new RuntimeEventDispatcher();
        var runtime = new PipelineRuntime(dispatcher);
        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };

        var result = await runtime.ExecuteAsync(
            "Parallel",
            [new StaticAgent("First", "one")],
            context,
            new ParallelPipelineExecutionStrategy());

        context.Items[PipelineContextKeys.RuntimeExecutionId]
            .Should()
            .Be(result.ExecutionId);
        context.Items[PipelineContextKeys.RuntimeEventDispatcher]
            .Should()
            .BeSameAs(dispatcher);

        dispatcher.Events.Select(e => e.GetType())
            .Should()
            .Equal(
                typeof(PipelineStartedEvent),
                typeof(PipelineCompletedEvent));

        dispatcher.Events.Select(e => e.ExecutionId)
            .Should()
            .OnlyContain(id => id == result.ExecutionId);
    }

    private sealed class StaticAgent : IAgent
    {
        private readonly string _response;

        public StaticAgent(
            string name,
            string response)
        {
            Name = name;
            _response = response;
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
        {
            context.CurrentOutput = _response;

            return Task.FromResult(
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        _response)));
        }

        public async IAsyncEnumerable<string> StreamAsync(
            string input,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            yield return _response;

            await Task.CompletedTask;
        }
    }
}
