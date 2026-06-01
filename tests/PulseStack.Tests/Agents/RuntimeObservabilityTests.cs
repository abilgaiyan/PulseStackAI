using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Agents.Runtime.Observability;
using Xunit;

namespace PulseStack.Tests.Agents;

public class RuntimeObservabilityTests
{
    [Fact]
    public void RuntimeEventDispatcher_Should_Notify_Observer()
    {
        var observer = new RecordingRuntimeObserver();
        var dispatcher = new RuntimeEventDispatcher(observer);
        var runtimeEvent = CreatePipelineStartedEvent();

        dispatcher.Dispatch(runtimeEvent);

        observer.Events.Should().ContainSingle()
            .Which.Should().Be(runtimeEvent);
    }

    [Fact]
    public async Task CompositeRuntimeObserver_Should_Notify_All_Observers_In_Order()
    {
        var first = new RecordingRuntimeObserver();
        var second = new RecordingRuntimeObserver();
        var observer = new CompositeRuntimeObserver([first, second]);
        var runtimeEvent = CreatePipelineStartedEvent();

        await observer.OnEventAsync(runtimeEvent);

        first.Events.Should().ContainSingle()
            .Which.Should().Be(runtimeEvent);
        second.Events.Should().ContainSingle()
            .Which.Should().Be(runtimeEvent);
    }

    [Fact]
    public async Task CompositeRuntimeObserver_Should_Swallow_Observer_Failures()
    {
        var healthy = new RecordingRuntimeObserver();
        var observer = new CompositeRuntimeObserver(
        [
            new ThrowingRuntimeObserver(),
            healthy
        ]);
        var runtimeEvent = CreatePipelineStartedEvent();

        var act = () => observer.OnEventAsync(runtimeEvent);

        await act.Should().NotThrowAsync();
        healthy.Events.Should().ContainSingle()
            .Which.Should().Be(runtimeEvent);
    }

    [Fact]
    public void RuntimeEventDispatcher_Should_Not_Break_When_Observer_Fails()
    {
        var dispatcher = new RuntimeEventDispatcher(
            new ThrowingRuntimeObserver());
        var runtimeEvent = CreatePipelineStartedEvent();

        var act = () => dispatcher.Dispatch(runtimeEvent);

        act.Should().NotThrow();
        dispatcher.Events.Should().ContainSingle()
            .Which.Should().Be(runtimeEvent);
    }

    [Fact]
    public async Task ConsoleRuntimeObserver_Should_Write_Readable_Event_Output()
    {
        var observer = new ConsoleRuntimeObserver();
        var output = new StringWriter();
        var originalOutput = Console.Out;

        try
        {
            Console.SetOut(output);

            await observer.OnEventAsync(
                new ToolExecutedEvent(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
                    "LookupInvoice",
                    "INV-100",
                    "Researcher",
                    null,
                    true,
                    null,
                    new Dictionary<string, object?>()));
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        output.ToString()
            .Should()
            .Contain("[Tool Executed]")
            .And.Contain("Tool : LookupInvoice")
            .And.Contain("Success : True");
    }

    [Fact]
    public async Task OpenTelemetryRuntimeObserver_Should_Emit_Runtime_Activities()
    {
        var activities = new List<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == "PulseStack.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
                activities.Add(activity)
        };

        ActivitySource.AddActivityListener(listener);

        var observer = new OpenTelemetryRuntimeObserver();
        var executionId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var branchId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        await observer.OnEventAsync(
            new PipelineStartedEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
                "ResearchPipeline",
                1,
                new Dictionary<string, object?>()));

        await observer.OnEventAsync(
            new AgentStartedEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:01Z"),
                "Researcher",
                "test-model",
                branchId,
                new Dictionary<string, object?>()));

        await observer.OnEventAsync(
            new AgentRetryEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:02Z"),
                "Researcher",
                1,
                "Transient failure."));

        await observer.OnEventAsync(
            new ToolExecutingEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:03Z"),
                "LookupInvoice",
                "INV-100",
                "Researcher",
                branchId,
                new Dictionary<string, object?>()));

        await observer.OnEventAsync(
            new ToolExecutedEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:04Z"),
                "LookupInvoice",
                "INV-100",
                "Researcher",
                branchId,
                true,
                null,
                new Dictionary<string, object?>(),
                "ERP",
                TimeSpan.FromMilliseconds(82)));

        await observer.OnEventAsync(
            new AgentCompletedEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:05Z"),
                "Researcher",
                "test-model",
                branchId,
                true,
                null,
                new Dictionary<string, object?>()));

        await observer.OnEventAsync(
            new PipelineCompletedEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:06Z"),
                "ResearchPipeline",
                1,
                1,
                0,
                new Dictionary<string, object?>()));

        activities.Select(activity => activity.OperationName)
            .Should()
            .Equal(
                "tool.execute",
                "agent.execute",
                "pipeline.execute");

        var pipeline = activities.Single(activity =>
            activity.OperationName == "pipeline.execute");
        pipeline.GetTagItem("pipeline.name").Should().Be("ResearchPipeline");
        pipeline.GetTagItem("execution.status").Should().Be("completed");
        pipeline.GetTagItem("successful.agents").Should().Be(1);
        pipeline.GetTagItem("failed.agents").Should().Be(0);

        var agent = activities.Single(activity =>
            activity.OperationName == "agent.execute");
        agent.GetTagItem("agent.name").Should().Be("Researcher");
        agent.GetTagItem("model").Should().Be("test-model");
        agent.GetTagItem("success").Should().Be(true);
        var retryEvent = agent.Events.Should().ContainSingle()
            .Which;
        retryEvent.Name.Should().Be("agent.retry");
        retryEvent.Tags.Single(tag => tag.Key == "attempt")
            .Value.Should().Be(1);
        retryEvent.Tags.Single(tag => tag.Key == "error")
            .Value.Should().Be("Transient failure.");

        var tool = activities.Single(activity =>
            activity.OperationName == "tool.execute");
        tool.GetTagItem("tool.name").Should().Be("LookupInvoice");
        tool.GetTagItem("tool.category").Should().Be("ERP");
        tool.GetTagItem("tool.duration.ms").Should().Be(82d);
        tool.GetTagItem("tool.success").Should().Be(true);
        tool.GetTagItem("success").Should().Be(true);
    }

    [Fact]
    public async Task OpenTelemetryRuntimeObserver_Should_Record_Failed_Agent_As_Error()
    {
        var activities = new List<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == "PulseStack.Runtime",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
                activities.Add(activity)
        };

        ActivitySource.AddActivityListener(listener);

        var observer = new OpenTelemetryRuntimeObserver();
        var executionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

        await observer.OnEventAsync(
            new AgentStartedEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
                "Broken",
                null,
                null,
                new Dictionary<string, object?>()));

        await observer.OnEventAsync(
            new AgentCompletedEvent(
                executionId,
                DateTimeOffset.Parse("2026-05-28T00:00:01Z"),
                "Broken",
                null,
                null,
                false,
                "Permanent failure.",
                new Dictionary<string, object?>()));

        var agent = activities.Should().ContainSingle()
            .Which;

        agent.OperationName.Should().Be("agent.execute");
        agent.Status.Should().Be(ActivityStatusCode.Error);
        agent.StatusDescription.Should().Be("Permanent failure.");
        agent.Events.Should().ContainSingle()
            .Which.Name.Should().Be("exception");
    }

    [Fact]
    public void AddOpenTelemetryRuntimeObserver_Should_Register_With_CompositeObserver()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetryRuntimeObserver()
            .AddConsoleRuntimeObserver();

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IRuntimeObserver>()
            .Select(observer => observer.GetType())
            .Should()
            .Contain([
                typeof(OpenTelemetryRuntimeObserver),
                typeof(ConsoleRuntimeObserver)
            ]);

        provider.GetRequiredService<CompositeRuntimeObserver>()
            .Should()
            .NotBeNull();
    }

    private static PipelineStartedEvent CreatePipelineStartedEvent()
        => new(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            DateTimeOffset.Parse("2026-05-28T00:00:00Z"),
            "ResearchPipeline",
            2,
            new Dictionary<string, object?>());

    private sealed class RecordingRuntimeObserver
        : IRuntimeObserver
    {
        public List<IRuntimeEvent> Events { get; } = [];

        public Task OnEventAsync(
            IRuntimeEvent runtimeEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(runtimeEvent);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRuntimeObserver
        : IRuntimeObserver
    {
        public Task OnEventAsync(
            IRuntimeEvent runtimeEvent,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Observer failed.");
    }
}
