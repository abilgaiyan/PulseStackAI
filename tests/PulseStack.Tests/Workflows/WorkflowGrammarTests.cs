using FluentAssertions;
using PulseStack.Tests.Workflows.Builders;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Builders;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowGrammarTests
{
    [Fact]
    public void End_Should_Return_To_Parent_Scope()
    {
        var agent1 = new FakeAgent("Step1", "Done1");
        var agent2 = new FakeAgent("Step2", "Done2");

        var workflow = 
            Workflow.Create("MainWorkflow")
                .Run(agent1)
                .Test("ValidationBlock")
                    .Run(agent2)
                .End()
            .Build();

        workflow.Steps.Should().HaveCount(2);

        // First step
        var runStep1 = workflow.Steps[0]
            .Should()
            .BeOfType<RunStep>()
            .Subject;

        runStep1.Agent.Should().BeSameAs(agent1);

        // Nested workflow
        var nestedWorkflow = workflow.Steps[1]
            .Should()
            .BeOfType<Workflow>()
            .Subject;

        nestedWorkflow.Name.Should().Be("ValidationBlock");

        nestedWorkflow.Steps.Should().ContainSingle();

        var runStep2 = nestedWorkflow.Steps[0]
            .Should()
            .BeOfType<RunStep>()
            .Subject;

        runStep2.Agent.Should().BeSameAs(agent2);
    }

    [Fact]
    public void If_Should_Return_IfConditionBuilder()
    {
        var builder =
                Workflow.Create("Approval")
                    .If(new DelegateCondition(_ => true));

        builder.Should().BeOfType<IfConditionBuilder<WorkflowBuilder>>();
    }

    [Fact]
    public void Then_Should_Return_ThenBuilder()
    {
        var builder =
            Workflow.Create("Approval")
                .If(new DelegateCondition(_ => true))
                    .Then();

        builder.Should().BeOfType<ThenBuilder<WorkflowBuilder>>();
    }

    [Fact]
    public void Else_Should_Return_ElseBuilder()
    {
        var builder =
            Workflow.Create("Approval")
                .If(new DelegateCondition(_ => true))
                    .Then()
                .Else();

        builder.Should().BeOfType<ElseBuilder<WorkflowBuilder>>();
    }
}