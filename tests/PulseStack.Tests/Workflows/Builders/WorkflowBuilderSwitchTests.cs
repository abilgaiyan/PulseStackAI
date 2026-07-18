using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Routing;
using PulseStack.Tests.Fakes;


namespace PulseStack.Tests.Workflows.Builders;

public class WorkflowBuilderSwitchTests
{
    [Fact]  
    public void WorkflowBuilder_Switch_Should_Use_Default_Name()
    {
        var cases = new[]
        {
            new SwitchCase("Approved", new FakeAgent("Approve", "Approved")),
            new SwitchCase("Rejected", new FakeAgent("Reject", "Rejected"))
        };

        var workflow = 
                Workflow.Create("Test")
                    .Switch(ctx => "Approved", cases)
                .Build();

        var switchStep = workflow.Steps.OfType<SwitchStep>().Single();

        switchStep.Name.Should().Be("Switch");
        switchStep.Selector.Should().NotBeNull();
        switchStep.Cases.Should().BeEquivalentTo(cases);
        switchStep.DefaultStep.Should().BeNull();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Use_Custom_Name()
    {
        var cases = new[]
        {
            new SwitchCase("admin", new FakeAgent("AdminPath", "Admin"))
        };

        var defaultStep = new RunStep(new FakeAgent("Default", "Default Path"));

        var workflow = 
                Workflow.Create("Test")
                    .Switch("UserRoleRouter", ctx => ctx.Items["Role"]?.ToString(), cases, defaultStep)
                .Build();

        var switchStep = workflow.Steps.OfType<SwitchStep>().Single();

        switchStep.Name.Should().Be("UserRoleRouter");
        switchStep.DefaultStep.Should().BeSameAs(defaultStep);
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Preserve_Cases()
    {
        var case1 = new SwitchCase("yes", new FakeAgent("Yes", "Yes"));
        var case2 = new SwitchCase("no", new FakeAgent("No", "No"));

        var workflow = 
                Workflow.Create("Test")
                    .Switch("Decision", _ => "yes", new[] { case1, case2 })
                .Build();

        var switchStep = workflow.Steps.OfType<SwitchStep>().Single();
        switchStep.Cases.Should().ContainInOrder(case1, case2);
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Support_Chaining_Both_Overloads()
    {
        var cases = new[] { new SwitchCase("A", new FakeAgent("A", "Done")) };

        var workflow = 
                Workflow.Create("Test")
                    .Switch(ctx => "A", cases)
                    .Switch("Another", ctx => "B", cases)
                .Build();

        workflow.Steps.OfType<SwitchStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Selector_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Switch(null!, new List<SwitchCase>())
                .Build();

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Cases_Is_Null()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Switch(_ => "test", null!)
                .Build();

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Cases_Is_Empty()
    {
        Action action = () => 
                Workflow.Create("Test")
                    .Switch(_ => "test", new List<SwitchCase>())
                .Build();

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Name_Is_Empty()
    {
        var cases = new[] { new SwitchCase("A", new FakeAgent("A", "Done")) };

        Action action = () => 
                Workflow.Create("Test")
                    .Switch("", _ => "A", cases)
                .Build();

        action.Should().Throw<ArgumentException>();
    }    

    [Fact]
    public void WorkflowBuilder_Switch_Should_Throw_When_Name_Is_Null()
    {
        var cases = new[]
        {
            new SwitchCase("A", new RunStep(new FakeAgent("A", "Done")))
        };

        Action action = () => 
                Workflow.Create("Test")
                    .Switch(
                        null!,           // name = null
                        _ => "A",
                        cases)
                .Build();

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Switch_Should_Preserve_Default_Step()
    {
        var defaultStep =
            new RunStep(
                new FakeAgent(
                    "Default",
                    "Done"));

        var workflow =
            Workflow.Create("Test")
                .Switch(
                    _ => "Unknown",
                    [
                        new SwitchCase(
                            "A",
                            new RunStep(new FakeAgent("A", "Done")))
                    ],
                    defaultStep)
            .Build();

        var step =
            workflow.Steps
                .OfType<SwitchStep>()
                .Single();

        step.DefaultStep
            .Should()
            .BeSameAs(defaultStep);
    }
}

