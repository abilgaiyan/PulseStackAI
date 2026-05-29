using FluentAssertions;
using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Core.Security;
using PulseStack.Core.Tools;
using PulseStack.Tests.Fakes;
using PulseStack.Tools.BuiltIn;
using Xunit;

namespace PulseStack.Tests.Agents;

public class RuntimeDiagnosticsTests
{
    [Fact]
    public async Task ParallelPipeline_Should_Publish_Pipeline_And_Agent_Lifecycle_Events()
    {
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new ParallelPipeline(
                "Parallel",
                dispatcher)
            .Add(new StaticAgent("First", "one"))
            .Add(new StaticAgent("Second", "two"));

        await pipeline.RunAsync("input");

        dispatcher.Events.Should().HaveCount(6);
        dispatcher.Events[0].Should().BeOfType<PipelineStartedEvent>();
        dispatcher.Events[1].Should().BeOfType<AgentStartedEvent>();
        dispatcher.Events[2].Should().BeOfType<AgentCompletedEvent>();
        dispatcher.Events[3].Should().BeOfType<AgentStartedEvent>();
        dispatcher.Events[4].Should().BeOfType<AgentCompletedEvent>();
        dispatcher.Events[5].Should().BeOfType<PipelineCompletedEvent>();

        var started = (PipelineStartedEvent)dispatcher.Events[0];
        var completed = (PipelineCompletedEvent)dispatcher.Events[5];

        completed.ExecutionId.Should().Be(started.ExecutionId);
        completed.SuccessfulAgentCount.Should().Be(2);
        completed.FailedAgentCount.Should().Be(0);
        completed.Timestamp.Should().BeOnOrAfter(started.Timestamp);

        dispatcher.Events
            .OfType<AgentStartedEvent>()
            .Select(e => e.ExecutionId)
            .Should()
            .OnlyContain(id => id == started.ExecutionId);

        dispatcher.Events
            .OfType<AgentCompletedEvent>()
            .Should()
            .OnlyContain(e => e.IsSuccess);
    }

    [Fact]
    public async Task SequentialPipeline_Should_Publish_Single_Lifecycle_Envelope_Around_Retry()
    {
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new SequentialPipeline(
                "Retry",
                new TestObserver(dispatcher))
            .WithPolicy(
                new PipelineExecutionPolicy
                {
                    MaxRetries = 2,
                    ContinueOnAgentFailure = false
                })
            .Add(new FlakyAgent("TransientValidator", "ok"));

        var result = await pipeline.RunDetailedAsync("input");

        dispatcher.Events.Select(e => e.GetType())
            .Should()
            .Equal(
                typeof(PipelineStartedEvent),
                typeof(AgentStartedEvent),
                typeof(AgentRetryEvent),
                typeof(AgentCompletedEvent),
                typeof(PipelineCompletedEvent));

        var retry = dispatcher.Events
            .OfType<AgentRetryEvent>()
            .Single();

        retry.AgentName.Should().Be("TransientValidator");
        retry.Attempt.Should().Be(1);

        var completed = dispatcher.Events
            .OfType<AgentCompletedEvent>()
            .Single();

        completed.AgentName.Should().Be("TransientValidator");
        completed.IsSuccess.Should().BeTrue();

        result.Steps.Should().ContainSingle();
        result.Steps[0].RetryCount.Should().Be(1);
        result.Steps[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task SequentialPipeline_Should_Publish_AgentCompleted_For_Final_Failure()
    {
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new SequentialPipeline(
                "Failure",
                new TestObserver(dispatcher))
            .WithPolicy(
                new PipelineExecutionPolicy
                {
                    MaxRetries = 1,
                    ContinueOnAgentFailure = true
                })
            .Add(new FailingAgent("Broken"));

        var result = await pipeline.RunDetailedAsync("input");

        dispatcher.Events.Select(e => e.GetType())
            .Should()
            .Equal(
                typeof(PipelineStartedEvent),
                typeof(AgentStartedEvent),
                typeof(AgentRetryEvent),
                typeof(AgentCompletedEvent),
                typeof(PipelineCompletedEvent));

        var completed = dispatcher.Events
            .OfType<AgentCompletedEvent>()
            .Single();

        completed.AgentName.Should().Be("Broken");
        completed.IsSuccess.Should().BeFalse();
        completed.ErrorMessage.Should().Be("Permanent failure.");

        result.Steps.Should().ContainSingle();
        result.Steps[0].Success.Should().BeFalse();
        result.Steps[0].RetryCount.Should().Be(1);
        result.Errors.Should().ContainSingle()
            .Which.Code.Should().Be("sequential_agent_execution_failed");
    }

    [Fact]
    public async Task AgentRuntime_Should_Publish_Agent_And_Tool_Lifecycle_In_Order()
    {
        var dispatcher = new RuntimeEventDispatcher();
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

        var runtime = new AgentRuntime(
            client,
            new ToolExecutor(new AllowAllToolAuthorizationService()),
            instructions: null,
            temperature: null,
            tools: registry,
            memory: null,
            model: "test-model",
            agent: null,
            eventDispatcher: dispatcher);

        var context = new PipelineContext
        {
            Input = "What is 5 * 5?",
            CurrentOutput = "What is 5 * 5?"
        };

        await runtime.RunAsync(context);

        dispatcher.Events.Select(e => e.GetType())
            .Should()
            .Equal(
                typeof(AgentStartedEvent),
                typeof(ToolExecutingEvent),
                typeof(ToolExecutedEvent),
                typeof(AgentCompletedEvent));

        var executionIds = dispatcher.Events
            .Select(e => e.ExecutionId)
            .Distinct()
            .ToList();

        executionIds.Should().ContainSingle();

        ((ToolExecutingEvent)dispatcher.Events[1]).ToolName
            .Should()
            .Be("calculator");
        ((ToolExecutedEvent)dispatcher.Events[2]).IsSuccess
            .Should()
            .BeTrue();
        ((AgentCompletedEvent)dispatcher.Events[3]).IsSuccess
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task ParallelPipeline_Should_Propagate_Execution_Metadata_To_AgentRuntime_Events()
    {
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new ParallelPipeline(
                "Parallel",
                dispatcher)
            .Add(new RuntimeBackedAgent("First", "one"))
            .Add(new RuntimeBackedAgent("Second", "two"));

        await pipeline.RunAsync("input");

        var pipelineStarted = dispatcher.Events
            .OfType<PipelineStartedEvent>()
            .Single();

        var agentStarted = dispatcher.Events
            .OfType<AgentStartedEvent>()
            .ToList();

        agentStarted.Should().HaveCount(2);
        agentStarted.Select(e => e.ExecutionId)
            .Should()
            .OnlyContain(id => id == pipelineStarted.ExecutionId);
        agentStarted.Select(e => e.BranchId)
            .Should()
            .OnlyContain(id => id.HasValue);

        dispatcher.Events.Last()
            .Should()
            .BeOfType<PipelineCompletedEvent>();
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

    private sealed class FlakyAgent : IAgent
    {
        private readonly string _response;
        private int _attempts;

        public FlakyAgent(
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
            _attempts++;

            if (_attempts == 1)
            {
                throw new InvalidOperationException("Transient failure.");
            }

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

    private sealed class FailingAgent : IAgent
    {
        public FailingAgent(
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
            => throw new InvalidOperationException("Permanent failure.");

        public async IAsyncEnumerable<string> StreamAsync(
            string input,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            yield break;
        }
    }

    private sealed class TestObserver : IRuntimeObserver
    {
        private readonly RuntimeEventDispatcher _dispatcher;

        public TestObserver(
            RuntimeEventDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public Task OnEventAsync(
            IRuntimeEvent runtimeEvent,
            CancellationToken cancellationToken = default)
        {
            _dispatcher.Dispatch(runtimeEvent);

            return Task.CompletedTask;
        }
    }

    private sealed class RuntimeBackedAgent : IAgent
    {
        private readonly AgentRuntime _runtime;

        public RuntimeBackedAgent(
            string name,
            string response)
        {
            Name = name;
            _runtime = new AgentRuntime(
                new FakeChatClient([response]),
                new ToolExecutor(new AllowAllToolAuthorizationService()),
                instructions: null,
                temperature: null,
                tools: null,
                memory: null,
                model: "test-model",
                agent: this,
                eventDispatcher: new RuntimeEventDispatcher());
        }

        public string Name { get; }

        public string? Model => "test-model";

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
            => _runtime.RunAsync(
                context,
                cancellationToken);

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
