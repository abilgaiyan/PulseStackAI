using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Tests.Fakes;


namespace PulseStack.Tests.Workflows.Builders;

public class WorkflowBuilderRetryTests
{
     [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Custom_Name()
    {
        var agent = new FakeAgent("ValidationStep", "Done");

        var workflow = 
                Workflow.Create("Research")
                    .Retry("Retry Validation",new RunStep(agent), 5)
                .Build();

        var retryStep = workflow.Steps.OfType<RetryStep>().Single();

        retryStep.Name.Should().Be("Retry Validation");
        retryStep.MaxAttempts.Should().Be(5);
        retryStep.Step.As<RunStep>().Agent.Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = 
                Workflow.Create("Test")
                    .Retry(new RunStep(new FakeAgent("A", "Done")), 2)
                    .Retry("CustomRetry", new RunStep(new FakeAgent("B", "Done")), 4)
                .Build();

        workflow.Steps.OfType<RetryStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Retry(null!, 3)
                .Build();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Retry("", new RunStep(new FakeAgent("A", "Done")), 3)
                .Build();

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_MaxAttempts_Is_Zero()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Retry(new RunStep(new FakeAgent("A", "Done")), 0)
                .Build();

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_MaxAttempts_Is_Negative()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Retry(new RunStep(new FakeAgent("A", "Done")), -1)
                .Build();

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Default_MaxAttempts()
    {
        var workflow =
            Workflow.Create("Test")
                .Retry(
                    new RunStep(new FakeAgent("A", "Done")))
                .Build();

        var retry =
            workflow.Steps
                .OfType<RetryStep>()
                .Single();

        retry.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Name_Is_Null()
    {
         Action action = () => 
                Workflow.Create("Test")
                    .Retry(null!,
                        new RunStep(new FakeAgent("A", "Done")))
                .Build();

        action.Should()
            .Throw<ArgumentException>();
    }

        [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Default_Name()
    {
        var agent = new FakeAgent("ValidationStep", "Done");

        var workflow = 
            Workflow.Create("Research")
                .Retry(new RunStep(agent), 3)
            .Build();

        var retryStep = workflow.Steps.OfType<RetryStep>().Single();

        retryStep.Name.Should().Be("Retry");
        retryStep.MaxAttempts.Should().Be(3);
        retryStep.Step.As<RunStep>().Agent.Should().BeSameAs(agent);
    }

}