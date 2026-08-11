using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using Xunit;

namespace PulseStack.Tests.Agents;

public class ParallelPipelineTests
{
    [Fact]
    public async Task RunAsync_Should_Execute_Agents_Concurrently()
    {
        var runningCount = 0;
        var bothStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new ParallelPipeline("Parallel", dispatcher)
            .Add(new BlockingAgent(
                "First",
                "one",
                () =>
                {
                    if (Interlocked.Increment(ref runningCount) == 2)
                    {
                        bothStarted.SetResult();
                    }

                    return release.Task;
                }))
            .Add(new BlockingAgent(
                "Second",
                "two",
                () =>
                {
                    if (Interlocked.Increment(ref runningCount) == 2)
                    {
                        bothStarted.SetResult();
                    }

                    return release.Task;
                }));

        var runTask = pipeline.RunAsync("input");

        var completed = await Task.WhenAny(
            bothStarted.Task,
            Task.Delay(TimeSpan.FromSeconds(1)));

        completed.Should().Be(
            bothStarted.Task,
            "both agents should start before either is released");

        release.SetResult();

        var result = await runTask;

        result.FinalOutput.Should().Be(
            string.Join(Environment.NewLine, "one", "two"));
    }

    [Fact]
    public async Task RunAsync_Should_Execute_Agents_In_Isolated_Branches()
    {
        var first = new TestAgent("First", "one");
        var second = new TestAgent("Second", "two");

        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };

        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new ParallelPipeline("Parallel", dispatcher)
            .Add(first)
            .Add(second);

        var result = await pipeline.RunAsync(context);

       // result.Success.Should().BeTrue();
        result.Steps.Should().HaveCount(2);

        result.Steps
            .Select(step => step.AgentName)
            .Should()
            .ContainInOrder("First", "Second");

        context.Items.Should().NotContainKey("branch");
    }

    [Fact]
    public async Task RunAsync_Should_Aggregate_Steps_And_FinalOutputs()
    {
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new ParallelPipeline("Parallel", dispatcher)
            .Add(new TestAgent("First", "one"))
            .Add(new TestAgent("Second", "two"));

        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };

        var result = await pipeline.RunAsync(context);

        result.FinalOutput.Should().Be(
            string.Join(Environment.NewLine, "one", "two"));

        result.Steps
            .Select(s => s.AgentName)
            .Should()
            .Equal("First", "Second");

        context.Items[
            PipelineContextKeys.AgentOutput("First")]
            .Should()
            .Be("one");

        context.Items[
            PipelineContextKeys.AgentOutput("Second")]
            .Should()
            .Be("two");
    }

    [Fact]
    public async Task RunAsync_Should_Preserve_Successful_Results_When_A_Branch_Fails()
    {
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new ParallelPipeline("Parallel", dispatcher)
            .Add(new TestAgent("First", "one"))
            .Add(new ThrowingAgent("Broken"));

        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };

        var result = await pipeline.RunAsync(context);

        result.FinalOutput.Should().Be("one");

        result.Steps
            .Select(s => s.AgentName)
            .Should()
            .Equal("First", "Broken");

        result.Steps
            .Single(s => s.AgentName == "Broken")
            .Success
            .Should()
            .BeFalse();

        context.Items
            .Should()
            .ContainKey(
                PipelineContextKeys.AgentError("Broken"));
    }

    private sealed class BlockingAgent : TestAgent
    {
        private readonly Func<Task> _block;

        public BlockingAgent(
            string name,
            string response,
            Func<Task> block)
            : base(name, response)
        {
            _block = block;
        }

        public async override Task<AgentResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            await _block();

            return await base.RunAsync(
                input,
                cancellationToken);
        }
    }

    private sealed class ThrowingAgent : TestAgent
    {
        public ThrowingAgent(string name)
            : base(name, string.Empty)
        {
        }

        public override Task<AgentResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Branch failed.");
    }

    private class TestAgent : IAgent
    {
        private readonly string _response;

        public TestAgent(
            string name,
            string response)
        {
            Name = name;
            _response = response;
        }

        public string Name { get; }

        public virtual Task<AgentResponse> RunAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new AgentResponse
                {
                    Text = _response
                });
        }

        public async IAsyncEnumerable<string> StreamAsync(
            string input,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return _response;

            await Task.CompletedTask;
        }
    }
}
