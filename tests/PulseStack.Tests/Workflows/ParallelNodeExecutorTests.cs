using FluentAssertions;
using Xunit;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Composition;
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

    [Fact]
    public async Task ParallelNode_Should_Return_Own_Name_And_Aggregate_Child_Usage()
    {
        var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        executors.Add(
            new FakeNodeExecutor(
                output: "A",
                usage: new AIUsage
                {
                    Provider = "Test",
                    Model = "model",
                    PromptTokens = 1,
                    CompletionTokens = 2
                }));

        var executor =
            new ParallelNodeExecutor(
                resolver);

        var node =
            new ParallelNode("Parallel")
                .Add(
                    new FakeAgent(
                        "A",
                        "Ignored"))
                .Add(
                    new FakeAgent(
                        "B",
                        "Ignored"));

        var result =
            await executor.ExecuteAsync(
                node,
                new PipelineContext());

        result.NodeName.Should().Be("Parallel");
        result.Success.Should().BeTrue();
        result.Output.Should().Be(
            string.Join(
                Environment.NewLine,
                "A",
                "A"));
        result.Usage.Should().NotBeNull();
        result.Usage!.PromptTokens.Should().Be(2);
        result.Usage.CompletionTokens.Should().Be(4);
    }
}
