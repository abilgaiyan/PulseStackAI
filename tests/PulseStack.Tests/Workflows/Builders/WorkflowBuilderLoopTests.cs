using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Agents;
using PulseStack.Tests.Fakes;


namespace PulseStack.Tests.Workflows.Builders;

public class WorkflowBuilderLoopTests
{
    [Fact]
    public void WorkflowBuilder_ForEach_Should_Use_Default_Name()
    {
        Func<PipelineContext, IEnumerable<object>> itemsSelector = 
            _ => [1, 2, 3 ];

        var processor = new RunStep(new FakeAgent("ProcessItem", "Processed"));

        var workflow = 
                Workflow.Create("Research")
                    .ForEach(itemsSelector, processor)
                .Build();

        var loopStep = workflow.Steps.OfType<LoopStep>().Single();

        loopStep.Name.Should().Be("ForEach");
        loopStep.Items.Should().BeSameAs(itemsSelector);
        loopStep.Step.Should().BeSameAs(processor);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Use_Custom_Name()
    {
        Func<PipelineContext, IEnumerable<object>> itemsSelector = 
            _ => [];

        var processor = new RunStep(new FakeAgent("ProcessItem", "Processed"));

        var workflow = 
                Workflow.Create("Research")
                    .ForEach("Process Documents", itemsSelector, processor)
                .Build();

        var loopStep = workflow.Steps.OfType<LoopStep>().Single();

        loopStep.Name.Should().Be("Process Documents");
        loopStep.Items.Should().BeSameAs(itemsSelector);
        loopStep.Step.Should().BeSameAs(processor);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = 
                Workflow.Create("Test")
                    .ForEach(_ => [ "a", "b" ], new RunStep(new FakeAgent("A", "Done")))
                    .ForEach("CustomLoop", _ => new[] { 1, 2 }.Select(x => (object)x), new RunStep(new FakeAgent("B", "Done")))
                .Build();

        workflow.Steps.OfType<LoopStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Items_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .ForEach(null!, new RunStep(new FakeAgent("A", "Done")))
                .Build();

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .ForEach(_ => [0], null!)
                .Build();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => 
            Workflow.Create("Test")
                .ForEach("", _ => [0], new RunStep(new FakeAgent("A", "Done")))
            .Build();
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Name_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .ForEach(null!, _ => [0], new RunStep(new FakeAgent("A", "Done")))
                .Build();

        action.Should().Throw<ArgumentException>();
    }
}