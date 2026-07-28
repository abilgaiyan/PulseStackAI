using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Showcase.Infrastructure;

namespace PulseStack.Showcase.Scenarios.Persistence;

internal static class ShowcaseWorkflowFactory
{
    public static Workflow CreateCustomerApprovalWorkflow()
    {
        var workflow = new Workflow(
        WorkflowIdentity.Create("1.0.0"),
        WorkflowStepId.New(),
        new WorkflowDefinition(
            "Customer Approval Workflow",
            "Sample workflow for persistence showcase"));

        workflow.Add(new RunStep(
            new SampleAgent("approval-agent", "Approval Agent")));

        workflow.Add(new RunStep(
            new SampleAgent("notification-agent", "Notification Agent")));

        return workflow;    
    }
}