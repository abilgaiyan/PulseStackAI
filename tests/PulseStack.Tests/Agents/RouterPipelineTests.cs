using FluentAssertions;
using PulseStack.Abstractions.Agents.Routing;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Routing;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Tests.Fakes;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using Xunit;

namespace PulseStack.Tests.Agents;

public class RouterPipelineTests
{
    [Fact]
    public async Task Router_Should_Select_Legal_Agent()
    {
        // Arrange

        var selector =
            new KeywordAgentSelector(
                new Dictionary<string, string>
                {
                    ["contract"] = "Legal"
                });

        var observer =
            new CompositeRuntimeObserver([]);

        var pipeline =
            new RouterPipeline(
                "Router",
                selector,
                observer)
            .Add(
                new FakeAgent(
                    "Legal",
                    "Legal Review"))
            .Add(
                new FakeAgent(
                    "Support",
                    "Support Response"));

        // Act

        var result =
            await pipeline.RunDetailedAsync(
                "Please review this contract.");

        // Assert

        result.Success.Should().BeTrue();

        result.Steps.Should().ContainSingle();

        result.Steps[0]
            .AgentName
            .Should()
            .Be("Legal");

        result.FinalOutput
            .Should()
            .Be("Legal Review");
    }

    [Fact]
    public async Task Router_Should_Select_First_Agent_When_No_Match()
    {
        // Arrange

        var selector =
            new KeywordAgentSelector(
                new Dictionary<string, string>
                {
                    ["contract"] = "Legal"
                });

        var observer =
            new CompositeRuntimeObserver([]);

        var pipeline =
            new RouterPipeline(
                "Router",
                selector,
                observer)
            .Add(
                new FakeAgent(
                    "Default",
                    "Default Response"))
            .Add(
                new FakeAgent(
                    "Legal",
                    "Legal Review"));

        // Act

        var result =
            await pipeline.RunDetailedAsync(
                "Monthly status report.");

        // Assert

        result.Success.Should().BeTrue();

        result.Steps.Should().ContainSingle();

        result.Steps[0]
            .AgentName
            .Should()
            .Be("Default");

        result.FinalOutput
            .Should()
            .Be("Default Response");
    }

    [Fact]
    public async Task Router_Should_Throw_When_No_Agents_Exist()
    {
        var selector =
            new KeywordAgentSelector(
                new Dictionary<string, string>());

        var observer =
            new CompositeRuntimeObserver([]);

        var pipeline =
            new RouterPipeline(
                "Router",
                selector,
                observer);

        Func<Task> act =
            async () =>
                await pipeline.RunDetailedAsync(
                    "contract");

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "Router contains no agents.");
    }

    [Fact]
    public async Task Router_Should_Emit_AgentSelectedEvent()
    {
        // Arrange

        var selector =
            new KeywordAgentSelector(
                new Dictionary<string, string>
                {
                    ["contract"] = "Legal"
                });

        var recordingObserver =
            new RecordingRuntimeObserver();

        var observer =
            new CompositeRuntimeObserver(
                [recordingObserver]);

        var pipeline =
            new RouterPipeline(
                "Router",
                selector,
                observer)
            .Add(
                new FakeAgent(
                    "Legal",
                    "Legal Review"))
            .Add(
                new FakeAgent(
                    "Support",
                    "Support Response"));

        // Act

        var result =
            await pipeline.RunDetailedAsync(
                "Please review this contract.");

        // Assert

        result.Success.Should().BeTrue();

        var selectionEvent =
            recordingObserver.Events
                .OfType<AgentSelectedEvent>()
                .Should()
                .ContainSingle()
                .Subject;

        selectionEvent.AgentName
            .Should()
            .Be("Legal");

        selectionEvent.SelectorName
            .Should()
            .Be(nameof(KeywordAgentSelector));
    }

    [Fact]
    public async Task Router_Should_Execute_Selected_Agent_Only()
    {
        var selector =
            new KeywordAgentSelector(
                new Dictionary<string, string>
                {
                    ["contract"] = "Legal"
                });

        var observer =
            new CompositeRuntimeObserver([]);

        var pipeline =
            new RouterPipeline(
                "Router",
                selector,
                observer)
            .Add(
                new FakeAgent(
                    "Legal",
                    "Legal Review"))
            .Add(
                new FakeAgent(
                    "Support",
                    "Support Response"));

        var result =
            await pipeline.RunDetailedAsync(
                "Please review this contract.");

        result.Steps
            .Should()
            .ContainSingle();

        result.Steps[0]
            .AgentName
            .Should()
            .Be("Legal");
    }    
    [Fact]
    public async Task Router_Should_Preserve_Usage_Tracking()
    {
        var selector =
            new KeywordAgentSelector(
                new Dictionary<string, string>
                {
                    ["contract"] = "Legal"
                });

        var observer =
            new CompositeRuntimeObserver([]);

        var pipeline =
            new RouterPipeline(
                "Router",
                selector,
                observer)
            .Add(
                new FakeAgent(
                    "Legal",
                    "Legal Review"))
            .Add(
                new FakeAgent(
                    "Support",
                    "Support Response"));

        var result =
            await pipeline.RunDetailedAsync(
                "Please review this contract.");

       result.TotalUsage.Should().NotBeNull();         
    }    
}