using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows.Builders;

public class ParallelBuilderTests
{
    [Fact]
    public void ParallelBuilder_Should_Create_ParallelStep_With_Multiple_Children()
    {
        var agent1 = new FakeAgent("Summarizer", "Summary");
        var agent2 = new FakeAgent("Classifier", "Classified");
        var agent3 = new FakeAgent("Sentiment", "Analyzed");

        var workflow = Workflow.Create("Research")
            .Parallel("AnalysisPhase")
                .Run(agent1)
                .Run(agent2)
                .Run(agent3)
            .End()
            .Build();

        var ParallelStep = workflow.Steps.OfType<ParallelStep>().Single();

        ParallelStep.Steps.Should().HaveCount(3);

        var runSteps = ParallelStep.Steps
            .Cast<RunStep>()
            .ToList();

        runSteps.Select(x => x.Agent)
            .Should()
            .ContainInOrder(agent1, agent2, agent3);
    }

    [Fact]
    public void ParallelBuilder_Should_Throw_When_No_Steps_Are_Added()
    {
        Action action = () => Workflow.Create("Test")
            .Parallel("EmptyParallel")
                // no .Run() or .Workflow()
            .End();

        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Parallel block requires at least one step.");
    }

    [Fact]
    public void ParallelBuilder_Should_Use_Default_Parallel_Name()
    {
        // If you want to allow default name in extension method
        var workflow = Workflow.Create("Test")
            .Parallel()                    // using default name from extension
                .Run(new FakeAgent("A", "Done"))
            .End()
            .Build();

        var parallel = workflow.Steps.OfType<ParallelStep>().Single();
        parallel.Name.Should().Be("Parallel");   // or default we set
    }

    [Fact]
    public void ParallelBuilder_Can_Be_Used_After_Other_Steps()
    {
        var workflow = Workflow.Create("Main")
            .Run(new FakeAgent("Initial", "Done"))
            .Parallel("Processing")
                .Run(new FakeAgent("Task1", "Done"))
                .Run(new FakeAgent("Task2", "Done"))
            .End()
            .Run(new FakeAgent("Final", "Done"))
            .Build();

        workflow.Steps.Should().HaveCount(3);
        workflow.Steps[1].Should().BeOfType<ParallelStep>();
    }

    [Fact]
    public void ParallelBuilder_Should_Allow_Nested_Workflows()
    {
        var child =
            Workflow.Create("Child")
                .Run(new FakeAgent("Child", "Done"))
                .Build();

        var workflow =
            Workflow.Create("Main")

                .Parallel()

                    .Workflow(child)

                .End()

                .Build();

        var parallel =
            workflow.Steps.OfType<ParallelStep>().Single();

        parallel.Steps.Should().ContainSingle();

        parallel.Steps[0].Should().BeSameAs(child);
    }

    private static RunStep ShouldBeRunStep(
        IWorkflowStep step,
        IAgent expected)
    {
        var runStep = step.Should()
            .BeOfType<RunStep>()
            .Subject;

        runStep.Agent.Should().BeSameAs(expected);

        return runStep;
    }
}