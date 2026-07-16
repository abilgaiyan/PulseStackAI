using FluentAssertions;
using Xunit;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Workflows;
public class ParallelStepExecutorTests
{
    [Fact]
    public async Task ParallelStep_Should_Execute_All_Steps()
    {
        var runtime =
            WorkflowTestRuntimeFactory.Create();

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new ParallelStep("Parallel")
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

        result.Steps.Should().ContainSingle();
    }

    [Fact]
    public async Task ParallelStep_Should_Aggregate_Output()
    {
        var runtime =
            WorkflowTestRuntimeFactory.Create();

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new ParallelStep("Parallel")
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
    public async Task ParallelStep_Should_Return_Own_Name_And_Aggregate_Child_Usage()
    {
        var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        executors.Add(
            new FakeStepExecutor(
                output: "A",
                usage: new AIUsage
                {
                    Provider = "Test",
                    Model = "model",
                    PromptTokens = 1,
                    CompletionTokens = 2
                }));

        var executor =
            new ParallelStepExecutor(
                resolver);

        var step =
            new ParallelStep("Parallel")
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
                step,
                new PipelineContext());

        result.StepName.Should().Be("Parallel");
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
