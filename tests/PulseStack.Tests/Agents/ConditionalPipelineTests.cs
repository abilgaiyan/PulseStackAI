using FluentAssertions;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Conditions;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Agents.Runtime.Diagnostics.Events;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Agents;

public class ConditionalPipelineTests
{
    [Fact]
    public async Task Condition_True_Uses_TrueBranch()
    {
        // Arrange

        var condition =
            new DelegateCondition(_ => true);

        var recording =
            new RecordingRuntimeObserver();
        var observer =
            new CompositeRuntimeObserver([recording]);

        var pipeline =
            new ConditionalPipeline(
                "Conditional",
                condition,
                observer)
            .AddTrueAgent(
                new FakeAgent(
                    "Compliance",
                    "Compliance Review"))
            .AddFalseAgent(
                new FakeAgent(
                    "Summary",
                    "Executive Summary"));

        // Act

        var result =
            await pipeline.RunAsync("input");

        // Assert

        result.FinalOutput
            .Should()
            .Be("Compliance Review");

        result.Steps
            .Select(x => x.AgentName)
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Be("Compliance");
    }

    [Fact]
    public async Task Condition_False_Uses_FalseBranch()
    {
        // Arrange

        var condition =
            new DelegateCondition(_ => false);

        var recording =
            new RecordingRuntimeObserver();    

        var observer =
            new CompositeRuntimeObserver([recording]);

        var pipeline =
            new ConditionalPipeline(
                "Conditional",
                condition,
                observer)
            .AddTrueAgent(
                new FakeAgent(
                    "Compliance",
                    "Compliance Review"))
            .AddFalseAgent(
                new FakeAgent(
                    "Summary",
                    "Executive Summary"));

        // Act

        var result =
            await pipeline.RunAsync("input");

        // Assert

        result.FinalOutput
            .Should()
            .Be("Executive Summary");

        result.Steps
            .Select(x => x.AgentName)
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Be("Summary");
    }

    [Fact]
    public async Task Condition_True_Should_Not_Execute_False_Branch()
    {
        var condition =
            new DelegateCondition(_ => true);

        var recording =
            new RecordingRuntimeObserver();
        var observer =
            new CompositeRuntimeObserver([recording]);

        var pipeline =
            new ConditionalPipeline(
                "Conditional",
                condition,
                observer)
            .AddTrueAgent(
                new FakeAgent(
                    "TrueAgent",
                    "TRUE"))
            .AddFalseAgent(
                new FakeAgent(
                    "FalseAgent",
                    "FALSE"));

        var result =
            await pipeline.RunDetailedAsync("input");

        result.Steps
            .Should()
            .ContainSingle();

        result.Steps[0]
            .AgentName
            .Should()
            .Be("TrueAgent");
    }

    [Fact]
    public async Task Condition_Should_Emit_Event()
    {
        // Arrange

        var recordingObserver =
            new RecordingRuntimeObserver();

        var observer =
            new CompositeRuntimeObserver(
                [recordingObserver]);

        var condition =
            new DelegateCondition(_ => true);

        var pipeline =
            new ConditionalPipeline(
                "Conditional",
                condition,
                observer)
            .AddTrueAgent(
                new FakeAgent(
                    "Compliance",
                    "Approved"));

        // Act

        await pipeline.RunDetailedAsync("input");

        // Assert

        var conditionEvent =
            recordingObserver.Events
                .OfType<ConditionEvaluatedEvent>()
                .Single();

        conditionEvent.Result
            .Should()
            .BeTrue();
    }
}
