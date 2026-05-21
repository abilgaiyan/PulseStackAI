using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Context;
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

        var pipeline = new ParallelPipeline("Parallel")
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
    public async Task RunAsync_Should_Use_Isolated_Branched_Contexts()
    {
        var first = new CapturingAgent("First", "one");
        var second = new CapturingAgent("Second", "two");
        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };

        var pipeline = new ParallelPipeline("Parallel")
            .Add(first)
            .Add(second);

        await pipeline.RunAsync(context);

        first.Context.Should().NotBeNull();
        second.Context.Should().NotBeNull();
        first.Context.Should().NotBeSameAs(context);
        second.Context.Should().NotBeSameAs(context);
        first.Context.Should().NotBeSameAs(second.Context);
        context.Items.Should().NotContainKey("branch");
    }

    [Fact]
    public async Task RunAsync_Should_Aggregate_Steps_ToolResults_And_FinalOutputs()
    {
        var pipeline = new ParallelPipeline("Parallel")
            .Add(new ToolRecordingAgent(
                "First",
                "one",
                "calculator"))
            .Add(new ToolRecordingAgent(
                "Second",
                "two",
                "clock"));

        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };

        var result = await pipeline.RunAsync(context);

        result.FinalOutput.Should().Be(
            string.Join(Environment.NewLine, "one", "two"));
        result.Steps.Select(s => s.AgentName)
            .Should()
            .Equal("First", "Second");
        context.ToolResults.Select(t => t.ToolName)
            .Should()
            .Equal("calculator", "clock");
        context.Items[PipelineContextKeys.AgentOutput("First")]
            .Should()
            .Be("one");
        context.Items[PipelineContextKeys.AgentOutput("Second")]
            .Should()
            .Be("two");
    }

    [Fact]
    public async Task RunAsync_Should_Preserve_Successful_Results_When_A_Branch_Fails()
    {
        var pipeline = new ParallelPipeline("Parallel")
            .Add(new ToolRecordingAgent(
                "First",
                "one",
                "calculator"))
            .Add(new ThrowingAgent("Broken"));

        var context = new PipelineContext
        {
            Input = "input",
            CurrentOutput = "input"
        };

        var result = await pipeline.RunAsync(context);

        result.FinalOutput.Should().Be("one");
        result.Steps.Should().ContainSingle()
            .Which.AgentName.Should().Be("First");
        context.ToolResults.Should().ContainSingle()
            .Which.ToolName.Should().Be("calculator");
        context.Items.Should().ContainKey(
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

        public override async Task<ChatResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            await _block();

            return await base.RunAsync(
                context,
                cancellationToken);
        }
    }

    private sealed class CapturingAgent : TestAgent
    {
        public CapturingAgent(
            string name,
            string response)
            : base(name, response)
        {
        }

        public PipelineContext? Context { get; private set; }

        public override Task<ChatResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            Context = context;
            context.Items["branch"] = Name;

            return base.RunAsync(
                context,
                cancellationToken);
        }
    }

    private sealed class ToolRecordingAgent : TestAgent
    {
        private readonly string _toolName;

        public ToolRecordingAgent(
            string name,
            string response,
            string toolName)
            : base(name, response)
        {
            _toolName = toolName;
        }

        public override Task<ChatResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            context.ToolResults.Add(
                new ToolExecutionRecord(
                    _toolName,
                    "input",
                    ToolExecutionResult.Success("ok")));

            return base.RunAsync(
                context,
                cancellationToken);
        }
    }

    private sealed class ThrowingAgent : TestAgent
    {
        public ThrowingAgent(string name)
            : base(name, string.Empty)
        {
        }

        public override Task<ChatResponse> RunAsync(
            PipelineContext context,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Branch failed.");
    }

    private abstract class TestAgent : IAgent
    {
        private readonly string _response;

        protected TestAgent(
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

        public virtual Task<ChatResponse> RunAsync(
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
