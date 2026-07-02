using FluentAssertions;
using PulseStack.Tests.Workflows.Builders;
using PulseStack.Abstractions.Runtime.Pipeline;
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

        var nestedWorkflow = workflow.Nodes[1] as WorkflowPipeline;
        nestedWorkflow.Should().NotBeNull();
        nestedWorkflow!.Name.Should().Be("ValidationBlock");
        nestedWorkflow.Nodes.Should().ContainSingle().Which.Should().BeSameAs(agent2);
    }
}