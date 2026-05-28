using FluentAssertions;
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
