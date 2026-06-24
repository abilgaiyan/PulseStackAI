using FluentAssertions;
using Xunit;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Workflows;
public class ParallelNodeExecutorTests
{
    [Fact]
    public async Task ParallelNode_Should_Execute_All_Nodes()
    {
        var runtime =
            WorkflowRuntimeFactory.Create();

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(
                    new ParallelNode("Parallel")
                        .Add(
                            new FakeAgent(
                                "Research",
                                "Research Complete"))
                        .Add(
                            new FakeAgent(
                                "Finance",
                                "Finance Complete")));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.Nodes.Should().ContainSingle();
    }

    [Fact]
    public async Task ParallelNode_Should_Aggregate_Output()
    {
        var runtime =
            WorkflowRuntimeFactory.Create();

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(
                    new ParallelNode("Parallel")
                        .Add(
                            new FakeAgent(
                                "Research",
                                "A"))
                        .Add(
                            new FakeAgent(
                                "Finance",
                                "B")));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Contain("A");
        result.FinalOutput.Should().Contain("B");
    }
}