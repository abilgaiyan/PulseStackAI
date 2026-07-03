using FluentAssertions;
using PulseStack.Abstractions.Workflow.Builders;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows.Builders;

public class ParallelBuilderTests
{
    [Fact]
    public void ParallelBuilder_Should_Create_ParallelNode_With_Multiple_Children()
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

        var parallelNode = workflow.Nodes.OfType<ParallelNode>().Single();

        parallelNode.Name.Should().Be("AnalysisPhase");
        parallelNode.Nodes.Should().HaveCount(3);
        parallelNode.Nodes.Should().ContainInOrder(agent1, agent2, agent3);
    }

    [Fact]
    public void ParallelBuilder_Should_Throw_When_No_Nodes_Are_Added()
    {
        Action action = () => Workflow.Create("Test")
            .Parallel("EmptyParallel")
                // no .Run() or .Workflow()
            .End();

        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Parallel block requires at least one node.");
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

        var parallel = workflow.Nodes.OfType<ParallelNode>().Single();
        parallel.Name.Should().Be("Parallel");   // or default we set
    }

    [Fact]
    public void ParallelBuilder_Can_Be_Used_After_Other_Nodes()
    {
        var workflow = Workflow.Create("Main")
            .Run(new FakeAgent("Initial", "Done"))
            .Parallel("Processing")
                .Run(new FakeAgent("Task1", "Done"))
                .Run(new FakeAgent("Task2", "Done"))
            .End()
            .Run(new FakeAgent("Final", "Done"))
            .Build();

        workflow.Nodes.Should().HaveCount(3);
        workflow.Nodes[1].Should().BeOfType<ParallelNode>();
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
            workflow.Nodes.OfType<ParallelNode>().Single();

        parallel.Nodes.Should().ContainSingle();

        parallel.Nodes[0].Should().BeSameAs(child);
    }
}