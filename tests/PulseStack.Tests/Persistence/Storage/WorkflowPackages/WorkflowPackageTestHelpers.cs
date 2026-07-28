using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Identity;
using PulseStack.Tests.Fakes;


namespace PulseStack.Tests.Persistence.Storage.WorkflowPackages;
internal static class WorkflowPackageTestHelpers
{
    public static Workflow CreateEmptyWorkflow(
        string name = "Empty Test Workflow",
        string? description = "For unit testing")
    {
        return new Workflow(
            WorkflowIdentity.Create("1.0.0"),
            WorkflowStepId.New(),
            new WorkflowDefinition(name, description));
    }

    public static Workflow CreateValidWorkflow(string name)
    {
        var workflow = CreateEmptyWorkflow(name);
        
        var agent1 = new FakeAgent("agent-alpha", "Agent Alpha");
        var agent2 = new FakeAgent("agent-beta", "Agent Beta");

        workflow.Add(new RunStep(agent1));
        workflow.Add(new RunStep(agent2));

        return workflow;
    }

    public static Workflow CreateWorkflowWithRunSteps()
    {
        var workflow = CreateEmptyWorkflow();

        var agent1 = new FakeAgent("agent-alpha", "Test Agent1");
        var agent2 = new FakeAgent("agent-beta", "Test Agent 2");

        workflow.Add(new RunStep(agent1));
        workflow.Add(new RunStep(agent2));

        return workflow;
    }

    public static WorkflowPackage CreatePackage(Workflow? workflow = null)
    {
        return new WorkflowPackage
        {
            Identity = new WorkflowPackageIdentity(WorkflowPackageId.New(), "1.0.0"),
            Metadata = new WorkflowPackageMetadata { Title = "Test Package" },
            Workflow = workflow ?? CreateWorkflowWithRunSteps()
        };
    }
}