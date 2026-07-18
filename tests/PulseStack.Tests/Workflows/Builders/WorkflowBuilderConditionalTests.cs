using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Tests.Fakes;

namespace PulseStack.Tests.Workflows.Builders;
public class WorkflowBuilderConditionalTests 
{

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Condition_Is_Null()
    {
        Action action = () => 
            Workflow.Create("Test")
                .If(null!)
                    .Then()
                       .Run((new FakeAgent("Test", "Done")))
                    .End()
                .Build();

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Step_Is_Null()
    {
        Action action = () => 
            Workflow.Create("Test")
                .If(new DelegateCondition(_ => true))
                    .Then()
                        .Run(null!)
                    .End()
                .Build();

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Use_Default_Name()
    {
        var condition = new DelegateCondition(_ => true);
        var fakeAgent = new FakeAgent("ApprovalStep", "Approved");

        var workflow = 
            Workflow.Create("Research")
                .Run(new FakeAgent("ResearchStep", "Done"))
                .If(condition)
                    .Then()
                        .Run(fakeAgent)
                    .End()           // overload without name
            .Build();

        var conditional = workflow.Steps.OfType<ConditionalStep>().Single();

        conditional.Name.Should().Be("If");
        conditional.Condition.Should().BeSameAs(condition);

        var thenWorkflow =
            conditional.ThenStep
                .Should()
                .BeOfType<Workflow>()
                .Subject;

        thenWorkflow.Steps.Should().ContainSingle();

        var runStep =
            thenWorkflow.Steps.Single()
                .Should()
                .BeOfType<RunStep>()
                .Subject;

        runStep.Agent.Should().BeSameAs(fakeAgent);
        
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Use_Custom_Name()
    {
        var condition = new DelegateCondition(_ => true);
        var agent = new FakeAgent("ApprovalStep", "Approved");

        var workflow = 
            Workflow.Create("Research")
                .If("IsUserApproved", condition)
                    .Then()
                        .Run(agent)   
                    .End()   // overload with name
                .Build();

        var conditional = workflow.Steps.OfType<ConditionalStep>().Single();
        conditional.Name.Should().Be("IsUserApproved");
        conditional.ThenStep
            .Should()
            .BeOfType<Workflow>();

        conditional.ThenStep.Children
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void WorkflowBuilder_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = 
            Workflow.Create("Test")
                .If(new DelegateCondition(_ => true))
                .Then()
                    .Run(new FakeAgent("If1", "Path1"))
                .End()
                .If("CheckPermission", new DelegateCondition(_ => false))
                    .Then()
                        .Run(new FakeAgent("If2", "Path2"))
                    .End()
            .Build();

        workflow.Steps.OfType<ConditionalStep>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Name_Is_Empty()
    {
        Action action =
            () => 
                Workflow.Create("Test")
                .If(
                    "",
                    new DelegateCondition(_ => true))
                    .Then()
                      .Run(new FakeAgent("A", "Done"))
                    .End()
                .Build();

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Name_Is_Null()
    {
        Action action =
            () => 
                Workflow.Create("Test")
                    .If(null!, new DelegateCondition(_ => true))
                    .Then()
                        .Run(new FakeAgent("A", "Done"))
                    .End()
                .Build();

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void If_Then_Should_Create_ConditionalStep()
    {
          var agent = new FakeAgent("FakeStep", "Fake Approved");

        var workflow =
            Workflow.Create("Test")
                .If(new DelegateCondition(_ => true))
                    .Then()
                        .Run(agent)
                    .End()
                .Build();

        var conditional =
            Assert.IsType<ConditionalStep>(
                workflow.Steps.Single());

        conditional.ThenStep.Should().NotBeNull();
        conditional.ElseStep.Should().BeNull();
    } 

    [Fact]
    public void If_Then_Else_Should_Create_Both_Branches()
    {
        var agent1 = new FakeAgent("FakeStep1", "Fake Approved1");
        var agent2 = new FakeAgent("FakeStep2", "Fake Approved2");
        var workflow =
            Workflow.Create("Test")
                .If(new DelegateCondition(_ => true))
                    .Then()
                        .Run(agent1)
                    .Else()
                        .Run(agent2)
                    .End()
                .Build();

        var conditional =
            Assert.IsType<ConditionalStep>(
                workflow.Steps.Single());

        conditional.ThenStep.Should().NotBeNull();

        conditional.ElseStep.Should().NotBeNull();
    }  

}