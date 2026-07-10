using FluentAssertions;
using Xunit;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Workflows;
public class RetryStepExecutorTests
{
    [Fact]
    public async Task RetryStep_Should_Execute_Successfully()
    {
                var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        executors.Add(
            WorkflowRuntimeFactory.CreateAgentExecutor());

        var executor =
            new RetryStepExecutor(
                resolver);

        var retryStep = new RetryStep(
            "Retry",
            new RunStep(new FakeAgent(
                "Researcher",
                "Done")));

        var result =
                    await executor.ExecuteAsync(
                        retryStep,
                        new PipelineContext());

        result.Success.Should().BeTrue();
        result.StepName.Should().Be("Retry");
        result.Output.Should().Be("Done");                
    }

    [Fact]
    public async Task RetryStep_Should_Return_Own_Name_And_Preserve_Final_Child_Result()
    {
        var usage =
            new AIUsage
            {
                Provider = "Test",
                Model = "model",
                PromptTokens = 5,
                CompletionTokens = 7
            };

        var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        executors.Add(
            new FakeStepExecutor(
                output: "Retried Output",
                usage: usage));

        var retryExecutor =
            new RetryStepExecutor(
                resolver);

        var step =
            new RetryStep(
                "Retry",
                new RunStep(new FakeAgent(
                    "Researcher",
                    "Executed")));

        var result =
            await retryExecutor.ExecuteAsync(
                step,
                new PipelineContext());

        result.StepName.Should().Be("Retry");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Retried Output");
        result.Usage.Should().BeSameAs(usage);
    }
    
    [Fact]
    public async Task RetryStep_Should_Retry_Until_Success()
    {
        var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        var flakyExecutor =
            new FlakyStepExecutor();

        executors.Add(flakyExecutor);

        var retryExecutor =
            new RetryStepExecutor(
                resolver);

        var step =
            new RetryStep(
                "Retry",
                new RunStep(new FakeAgent(
                    "Researcher",
                    "Executed")),
                maxAttempts: 3);

        var result =
            await retryExecutor.ExecuteAsync(
                step,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.Output.Should().Be("Success");

        flakyExecutor.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task RetryStep_Should_Stop_After_Max_Attempts()
    {
        var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        var failingExecutor =
            new AlwaysFailStepExecutor();

        executors.Add(failingExecutor);

        var retryExecutor =
            new RetryStepExecutor(
                resolver);

        var step =
            new RetryStep(
                "Retry",
                new RunStep(new FakeAgent(
                    "Researcher",
                    "Executed")),
                maxAttempts: 3);

        var result =
            await retryExecutor.ExecuteAsync(
                step,
                new PipelineContext());

        result.Success.Should().BeFalse();

        failingExecutor.Attempts.Should().Be(3);
    }
}
