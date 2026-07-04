using FluentAssertions;
using PulseStack.Tests.Workflows.Builders;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Abstractions.Workflow.Builders;
using PulseStack.Abstractions.Workflow.Conditions;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowLanguageGrammarTests
{
    [Fact]
    public void End_Should_Return_To_Parent_Scope()
    {
        var agent1 = new FakeAgent("Step1", "Done1");
        var agent2 = new FakeAgent("Step2", "Done2");

        var workflow = Workflow.Create("MainWorkflow")
            .Run(agent1)

            .Test("ValidationBlock")      // Enter composite scope
                .Run(agent2)
            .End()                        // Exit composite scope

            .Build();

        workflow.Nodes.Should().HaveCount(2);
        workflow.Nodes[0].Should().BeSameAs(agent1);

        var nestedWorkflow = workflow.Nodes[1] as WorkflowDefinition;
        nestedWorkflow.Should().NotBeNull();
        nestedWorkflow!.Name.Should().Be("ValidationBlock");
        nestedWorkflow.Nodes.Should().ContainSingle().Which.Should().BeSameAs(agent2);
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