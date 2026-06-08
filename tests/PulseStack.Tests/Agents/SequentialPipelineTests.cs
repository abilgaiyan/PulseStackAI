using FluentAssertions;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Context;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Agents;

public class SequentialPipelineTests
{
     [Fact]
    public async Task RunAsync_Should_Execute_Agents_In_Order()
    {
        // Arrange

        var pipeline =
            new SequentialPipeline("Sequential")
                .Add(new FakeAgent("First", "one"))
                .Add(new FakeAgent("Second", "two"))
                .Add(new FakeAgent("Third", "three"));

        // Act

        var result = await pipeline.RunDetailedAsync("input");

        // Assert
        result.Steps
            .Select(x => x.AgentName)
            .Should()
            .Equal(
            [
                "First",
                "Second",
                "Third"
            ]);

        result.FinalOutput
            .Should()
            .Be("three");
    }

    [Fact]
    public async Task SequentialPipeline_Should_Throw_When_No_Agents_Exist()
    {
        var pipeline = new SequentialPipeline("Empty");

        Func<Task> act = async () => await pipeline.RunAsync("input");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Pipeline contains no agents.");
    }

    [Fact]
    public async Task RunAsync_Should_Return_Last_Agent_Output()
    {
        // Arrange
        var pipeline =
            new SequentialPipeline("Sequential")
                .Add(new FakeAgent("Researcher", "research"))
                .Add(new FakeAgent("Summarizer", "summary"));

        // Act
        var result =
            await pipeline.RunDetailedAsync("input");

        // Assert
        result.FinalOutput
            .Should()
            .Be("summary");
    }

    [Fact]
    public async Task RunAsync_Should_Execute_RecordingAgents_In_Order()
    {
        var executionOrder = new List<string>();
        var dispatcher = new RuntimeEventDispatcher();

        var pipeline = new SequentialPipeline("Sequential", dispatcher)
            .Add(new RecordingAgent("First", executionOrder))
            .Add(new RecordingAgent("Second", executionOrder))
            .Add(new RecordingAgent("Third", executionOrder));

        await pipeline.RunAsync("input");

        executionOrder.Should().Equal(
            ["First", "Second", "Third"],
            "agents should execute in the order they were added");
    }

    [Fact]
    public async Task SequentialPipeline_Should_Record_Model()
    {
        var pipeline =
            new SequentialPipeline("Sequential")
                .Add(new FakeAgent(
                    "Researcher",
                    "Research",
                    "gpt-4"));

        var result =
            await pipeline.RunDetailedAsync("input");

        result.Steps[0].Model
            .Should()
            .Be("gpt-4");
    }

}