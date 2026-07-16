using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Workflows.Builders;

public class WorkflowBuilderParallelTests
{
    [Fact]
    public void WorkflowBuilder_Parallel_Should_Use_Default_Name()
    {
        var step1 = new RunStep(new FakeAgent("A", "Done"));
        var step2 = new RunStep(new FakeAgent("B", "Done"));

        var workflow = 
                Workflow.Create("Test")
                    .Parallel(step1, step2)
                .Build();

        var parallel = workflow.Steps.OfType<ParallelStep>().Single();

        parallel.Name.Should().Be("Parallel");
        parallel.Steps.Should().HaveCount(2);
        parallel.Steps.Should().Contain(step1);
        parallel.Steps.Should().Contain(step2);
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Use_Custom_Name()
    {
        var step1 = new RunStep(new FakeAgent("Summarizer", "Done"));
        var step2 = new RunStep(new FakeAgent("Classifier", "Done"));

        var workflow = 
                Workflow.Create("Test")
                    .Parallel("Analysis", step1, step2)
                .Build();

        var parallel = workflow.Steps.OfType<ParallelStep>().Single();
        parallel.Name.Should().Be("Analysis");
        parallel.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = 
                Workflow.Create("Test")
                    .Parallel(new RunStep(new FakeAgent("A", "Done")))
                    .Parallel("Custom", new RunStep(new FakeAgent("B", "Done")), new RunStep(new FakeAgent("C", "Done")))
                .Build();

        workflow.Steps.OfType<ParallelStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Array_Is_Empty()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Parallel(Array.Empty<IWorkflowStep>())
                .Build();

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Parallel(new RunStep(new FakeAgent("A", "Done")), null!)
                .Build();

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Parallel("", new RunStep(new FakeAgent("A", "Done")))
                .Build();

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Throw_When_Name_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Parallel(
                        name:null!,
                        steps: new RunStep(new FakeAgent("A", "Done")))
                    .Build();

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Parallel_Should_Preserve_Step_Order()
    {
        var step1 =
            new RunStep(new FakeAgent("A", "Done"));

        var step2 =
            new RunStep(new FakeAgent("B", "Done"));

        var step3 =
            new RunStep(new FakeAgent("C", "Done"));

        var workflow =
            Workflow.Create("Test")
                .Parallel(step1, step2, step3)
            .Build();

        var parallel =
            workflow.Steps
                .OfType<ParallelStep>()
                .Single();

        parallel.Steps.Should().ContainInOrder(
            step1,
            step2,
            step3);
    }

}
