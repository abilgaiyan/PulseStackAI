using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Identity;

namespace PulseStack.Showcase.Scenarios.Persistence;
internal static class ShowcasePackageFactory
{
    public static WorkflowPackage CreatePackage()
    {
        return new WorkflowPackage
        {
            Identity = new WorkflowPackageIdentity(WorkflowPackageId.New(), "1.0.0"),
            Metadata = new WorkflowPackageMetadata { Title = "Sample Package" },
            Workflow =
                ShowcaseWorkflowFactory
                    .CreateCustomerApprovalWorkflow()
        };
    }
}