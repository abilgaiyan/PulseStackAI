namespace PulseStack.Abstractions.WorkflowPackages.Identity;

public static class WorkflowPackageIdExtensions
{
    public static WorkflowPackageId EnsureValid(this WorkflowPackageId id)
    {
        if (id.IsEmpty)
        {
            throw new InvalidOperationException("Workflow package id cannot be empty.");
        }

        return id;
    }
}
