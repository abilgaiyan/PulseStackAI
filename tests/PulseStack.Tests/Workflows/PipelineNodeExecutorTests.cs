using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class PipelineNodeExecutorTests
{
    // [Fact]
    // public async Task PipelineNodeExecutor_Should_Execute_SequentialPipeline()
    // {
    //     // Arrange

    //     var dispatcher =
    //         new RuntimeEventDispatcher();

    //     var executor =
    //         new PipelineStepExecutor();

    //     var pipeline =
    //         new SequentialPipeline(
    //             "Research Pipeline",
    //             dispatcher)
    //         .Add(
    //             new FakeAgent(
    //                 "Researcher",
    //                 "Research Complete"));

    //     var context =
    //         new PipelineContext
    //         {
    //             Input = "AI orchestration"
    //         };

    //     // Act

    //     var result =
    //         await executor.ExecuteAsync(
    //             pipeline,
    //             context);

    //     // Assert

    //     result.Success.Should().BeTrue();

    //     result.StepName.Should().Be(
    //         "Research Pipeline");

    //     result.Output.Should().Be(
    //         "Research Complete");
    // }
}
